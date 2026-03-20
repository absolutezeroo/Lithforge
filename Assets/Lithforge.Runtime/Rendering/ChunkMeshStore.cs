using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Lithforge.Meshing;
using Lithforge.Runtime.Debug;
using Lithforge.Voxel.Chunk;

using Unity.Collections;
using Unity.Mathematics;

using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

using ILogger = Lithforge.Core.Logging.ILogger;

namespace Lithforge.Runtime.Rendering
{
    /// <summary>
    ///     Stores chunk meshes in three multi-arena GPU buffer pools (opaque, cutout, translucent)
    ///     and draws them via GPU-driven per-chunk indirect draw with compute frustum culling.
    ///     Each pool manages 1-N BufferArena instances to stay within the per-buffer GraphicsBuffer
    ///     size limit (SystemInfo.maxGraphicsBufferSize / 4, capped at 512 MB per arena).
    ///     Each chunk gets its own DrawIndexedIndirectArgs entry. A compute shader tests AABBs
    ///     against the camera frustum and sets instanceCount=0 for culled chunks.
    ///     No GameObjects, MeshFilters, MeshRenderers, or Mesh objects are created.
    ///     Shaders read vertex data from StructuredBuffer via SV_VertexID.
    ///     Owner: LithforgeBootstrap. Lifetime: application session.
    /// </summary>
    public sealed class ChunkMeshStore : IDisposable
    {
        /// <summary>Shader property ID for the vertex StructuredBuffer binding.</summary>
        private static readonly int s_vertexBufferId = Shader.PropertyToID("_VertexBuffer");

        /// <summary>Shader property ID for the chunk bounds StructuredBuffer in the cull shader.</summary>
        private static readonly int s_chunkBoundsBufferId = Shader.PropertyToID("_ChunkBoundsBuffer");

        /// <summary>Shader property ID for the per-chunk indirect args buffer in the cull shader.</summary>
        private static readonly int s_perChunkArgsBufferId = Shader.PropertyToID("_PerChunkArgsBuffer");

        /// <summary>Shader property ID for the frustum plane vector array in the cull shader.</summary>
        private static readonly int s_frustumPlanesId = Shader.PropertyToID("_FrustumPlanes");

        /// <summary>Shader property ID for the active slot count uniform in the cull shader.</summary>
        private static readonly int s_slotCountId = Shader.PropertyToID("_SlotCount");

        /// <summary>Shader property ID for the Hi-Z pyramid texture in the occlusion cull shader.</summary>
        private static readonly int s_hiZTextureId = Shader.PropertyToID("_HiZTexture");

        /// <summary>Shader property ID for the view-projection matrix in the occlusion cull shader.</summary>
        private static readonly int s_viewProjMatrixId = Shader.PropertyToID("_ViewProjMatrix");

        /// <summary>Shader property ID for the Hi-Z texture dimensions in the occlusion cull shader.</summary>
        private static readonly int s_hiZSizeId = Shader.PropertyToID("_HiZSize");

        /// <summary>Shader property ID for the Hi-Z mip level count in the occlusion cull shader.</summary>
        private static readonly int s_hiZMipCountId = Shader.PropertyToID("_HiZMipCount");

        /// <summary>Very large bounds so URP never frustum-culls the procedural draws.</summary>
        private static readonly Bounds s_worldBounds = new(Vector3.zero, new Vector3(100000f, 100000f, 100000f));

        /// <summary>Single-element array for uploading one chunk's AABB to the bounds buffer.</summary>
        private readonly ChunkBoundsGPU[] _boundsUpload = new ChunkBoundsGPU[1];

        /// <summary>Maps chunk coordinate to its cull slot ID (dense 0-based index for compute dispatch).</summary>
        private readonly Dictionary<int3, int> _coordToCullSlot = new();

        /// <summary>Render parameters for the cutout (alpha-test) indirect draw call.</summary>
        private readonly RenderParams _cutoutParams;

        /// <summary>Compute kernel index for the frustum culling pass.</summary>
        private readonly int _frustumCullKernel;

        /// <summary>Compute shader that performs frustum and occlusion culling on per-chunk indirect args.</summary>
        private readonly ComputeShader _frustumCullShader;

        /// <summary>Cached camera frustum planes for extraction into Vector4 format.</summary>
        private readonly Plane[] _frustumPlanes = new Plane[6];

        /// <summary>Cached frustum plane vectors to avoid per-frame array allocation.</summary>
        private readonly Vector4[] _frustumPlaneVectors = new Vector4[6];

        /// <summary>Hi-Z mipmap pyramid for GPU occlusion culling, or null if Hi-Z shader was not provided.</summary>
        private readonly HiZPyramid _hiZPyramid;

        /// <summary>Compute kernel index for the combined frustum + Hi-Z occlusion culling pass.</summary>
        private readonly int _occlusionCullKernel;

        /// <summary>Render parameters for the opaque indirect draw call.</summary>
        private readonly RenderParams _opaqueParams;

        /// <summary>Compute kernel index for the reset pass that restores instanceCount before culling.</summary>
        private readonly int _resetCullKernel;

        /// <summary>GPU buffer resize service — dispatches compute copy and defers disposal.</summary>
        private readonly GpuBufferResizer _resizer;

        /// <summary>Render parameters for the translucent (water) indirect draw call.</summary>
        private readonly RenderParams _translucentParams;

        /// <summary>
        ///     Shared bounds buffer: same AABB regardless of render layer.
        ///     Cull slot IDs index into this buffer.
        /// </summary>
        private GraphicsBuffer _chunkBoundsBuffer;

        /// <summary>Maximum number of concurrent chunk cull slots, grown by doubling when exceeded.</summary>
        private int _maxChunkSlots;

        /// <summary>Logger for mesh store diagnostics.</summary>
        private readonly ILogger _logger;

        /// <summary>Reverse mapping from cull slot ID to chunk coordinate, used for swap-and-pop on destroy.</summary>
        private int3[] _slotToCoord;

        /// <summary>
        ///     Creates a ChunkMeshStore with three BufferArenaPools (opaque, cutout, translucent),
        ///     a shared per-chunk bounds buffer, and configures GPU frustum and occlusion culling.
        ///     Buffer capacities are estimated from render distance and Y load range. Each pool
        ///     starts with one arena and creates additional arenas on demand when capacity is exceeded.
        /// </summary>
        public ChunkMeshStore(
            Material opaqueMaterial, Material cutoutMaterial, Material translucentMaterial,
            int renderDistance, int yLoadMin, int yLoadMax,
            int yUnloadMin, int yUnloadMax,
            ComputeShader frustumCullShader, ComputeShader hiZGenerateShader,
            GpuBufferResizer resizer,
            IPipelineStats pipelineStats,
            ILogger logger = null)
        {
            OpaqueMaterial = opaqueMaterial;
            CutoutMaterial = cutoutMaterial;
            TranslucentMaterial = translucentMaterial;
            _resizer = resizer;
            _logger = logger;

            // Compute max chunk slots from unload boundaries (worst-case loaded chunks).
            // Unload radius is renderDistance + 1 on XZ; Y uses the unload range.
            int unloadDiameter = (renderDistance + 1) * 2 + 1;
            int yUnloadLevels = yUnloadMax - yUnloadMin + 1;
            int maxLoadedChunks = unloadDiameter * unloadDiameter * yUnloadLevels;
            int maxChunkSlots = (maxLoadedChunks + 63) / 64 * 64; // align to compute group size
            maxChunkSlots = math.max(maxChunkSlots, 256);
            _maxChunkSlots = maxChunkSlots;
            _frustumCullShader = frustumCullShader;

            if (_frustumCullShader != null)
            {
                _resetCullKernel = _frustumCullShader.FindKernel("CSResetCull");
                _frustumCullKernel = _frustumCullShader.FindKernel("CSFrustumCull");

                if (hiZGenerateShader != null)
                {
                    _hiZPyramid = new HiZPyramid(hiZGenerateShader);
                    _occlusionCullKernel = _frustumCullShader.FindKernel("CSOcclusionCull");
                }
            }

            // Build RenderParams with MaterialPropertyBlock for buffer binding.
            _opaqueParams = new RenderParams(opaqueMaterial)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = 0,
                worldBounds = s_worldBounds,
                matProps = new MaterialPropertyBlock(),
            };
            _cutoutParams = new RenderParams(cutoutMaterial)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = 0,
                worldBounds = s_worldBounds,
                matProps = new MaterialPropertyBlock(),
            };
            _translucentParams = new RenderParams(translucentMaterial)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = 0,
                worldBounds = s_worldBounds,
                matProps = new MaterialPropertyBlock(),
            };

            // Estimate buffer sizes from render distance.
            // Average ~3000 opaque verts/chunk, ~4500 indices/chunk.
            // Cutout/translucent are ~10% of opaque.
            // +50% headroom for chunk churn (old slots not yet compacted).
            int diameter = renderDistance * 2 + 1;
            int yLevels = yLoadMax - yLoadMin + 1;
            int maxChunks = diameter * diameter * yLevels;
            int opaqueVerts = maxChunks * 3000 * 3 / 2;
            int opaqueIdx = maxChunks * 4500 * 3 / 2;
            int smallVerts = maxChunks * 300 * 3 / 2;
            int smallIdx = maxChunks * 450 * 3 / 2;

            OpaquePool = new BufferArenaPool("Opaque", opaqueVerts, opaqueIdx, maxChunkSlots, resizer, pipelineStats);
            CutoutPool = new BufferArenaPool("Cutout", smallVerts, smallIdx, maxChunkSlots, resizer, pipelineStats);
            TranslucentPool = new BufferArenaPool("Translucent", smallVerts, smallIdx, maxChunkSlots, resizer, pipelineStats);

            // Shared chunk bounds buffer — same AABB regardless of render layer.
            _chunkBoundsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured | GraphicsBuffer.Target.Raw,
                maxChunkSlots,
                Marshal.SizeOf<ChunkBoundsGPU>());

            // Zero-initialize bounds
            ChunkBoundsGPU[] zeroBounds = new ChunkBoundsGPU[maxChunkSlots];
            _chunkBoundsBuffer.SetData(zeroBounds);

            _slotToCoord = new int3[maxChunkSlots];
        }

        /// <summary>Number of active chunk cull slots (contiguous in 0..RendererCount-1).</summary>
        public int RendererCount { get; private set; }

        /// <summary>Multi-arena buffer pool for opaque chunk mesh data.</summary>
        public BufferArenaPool OpaquePool { get; }

        /// <summary>Multi-arena buffer pool for cutout (alpha-test) chunk mesh data.</summary>
        public BufferArenaPool CutoutPool { get; }

        /// <summary>Multi-arena buffer pool for translucent (water) chunk mesh data.</summary>
        public BufferArenaPool TranslucentPool { get; }

        /// <summary>Material used for opaque voxel rendering.</summary>
        public Material OpaqueMaterial { get; }

        /// <summary>Material used for cutout (alpha-test) voxel rendering.</summary>
        public Material CutoutMaterial { get; }

        /// <summary>Material used for translucent (water) voxel rendering.</summary>
        public Material TranslucentMaterial { get; }

        /// <summary>Whether Hi-Z occlusion culling is active (pyramid generated successfully).</summary>
        public bool IsOcclusionCullingActive
        {
            get
            {
                return _hiZPyramid is
                {
                    IsValid: true,
                };
            }
        }

        /// <summary>Releases all GPU buffers, CPU mirrors, and the Hi-Z pyramid.</summary>
        public void Dispose()
        {
            _hiZPyramid?.Dispose();
            _coordToCullSlot.Clear();
            _slotToCoord = null;
            RendererCount = 0;
            OpaquePool.Dispose();
            CutoutPool.Dispose();
            TranslucentPool.Dispose();
            _chunkBoundsBuffer?.Dispose();
        }

        /// <summary>
        ///     Updates or creates a chunk's mesh data with 3-submesh data (LOD0).
        ///     Called by MeshScheduler.PollCompleted.
        /// </summary>
        public void UpdateRenderer(
            int3 coord,
            NativeList<PackedMeshVertex> opaqueVerts, NativeList<int> opaqueIndices,
            NativeList<PackedMeshVertex> cutoutVerts, NativeList<int> cutoutIndices,
            NativeList<PackedMeshVertex> translucentVerts, NativeList<int> translucentIndices)
        {
            OpaquePool.AllocateOrUpdate(coord, opaqueVerts, opaqueIndices);
            CutoutPool.AllocateOrUpdate(coord, cutoutVerts, cutoutIndices);
            TranslucentPool.AllocateOrUpdate(coord, translucentVerts, translucentIndices);

            int cullSlotId = EnsureCullSlotId(coord);
            OpaquePool.UpdatePerChunkArgs(cullSlotId, coord);
            CutoutPool.UpdatePerChunkArgs(cullSlotId, coord);
            TranslucentPool.UpdatePerChunkArgs(cullSlotId, coord);
            UpdateChunkBounds(coord, cullSlotId);
        }

        /// <summary>
        ///     Updates or creates a chunk's mesh data with a single opaque submesh (LOD).
        ///     Called by LODScheduler.PollCompleted.
        /// </summary>
        public void UpdateRendererSingleMesh(
            int3 coord,
            NativeList<PackedMeshVertex> vertices, NativeList<int> indices)
        {
            OpaquePool.AllocateOrUpdate(coord, vertices, indices);
            CutoutPool.Free(coord);
            TranslucentPool.Free(coord);

            int cullSlotId = EnsureCullSlotId(coord);
            OpaquePool.UpdatePerChunkArgs(cullSlotId, coord);
            CutoutPool.ZeroPerChunkArgs(cullSlotId);
            TranslucentPool.ZeroPerChunkArgs(cullSlotId);
            UpdateChunkBounds(coord, cullSlotId);
        }

        /// <summary>
        ///     Ensures a cull slot ID exists for the given chunk coordinate.
        ///     New chunks are appended at RendererCount (contiguous packing).
        ///     Grows the slot infrastructure if capacity is exceeded.
        /// </summary>
        private int EnsureCullSlotId(int3 coord)
        {
            if (_coordToCullSlot.TryGetValue(coord, out int slotId))
            {
                return slotId;
            }

            if (RendererCount >= _maxChunkSlots)
            {
                GrowSlotCapacity();
            }

            slotId = RendererCount;
            _slotToCoord[RendererCount] = coord;
            RendererCount++;
            _coordToCullSlot[coord] = slotId;
            return slotId;
        }

        /// <summary>
        ///     Doubles the slot capacity for per-chunk indirect draw. Grows the shared
        ///     bounds buffer and per-chunk args in all three BufferArenaPools via GPU
        ///     compute copy (no blocking GetData readback). Old buffers are retired for
        ///     deferred disposal.
        /// </summary>
        private void GrowSlotCapacity()
        {
            int oldMax = _maxChunkSlots;
            int newMax = (oldMax * 2 + 63) / 64 * 64;

            // Grow per-chunk args in all 3 pools (each arena's args buffer is resized)
            OpaquePool.GrowSlots(newMax);
            CutoutPool.GrowSlots(newMax);
            TranslucentPool.GrowSlots(newMax);

            // Grow chunk bounds buffer via compute copy (no GetData blocking readback)
            _chunkBoundsBuffer = _resizer.Resize(
                _chunkBoundsBuffer,
                newMax,
                oldMax,
                GraphicsBuffer.Target.Structured | GraphicsBuffer.Target.Raw,
                Marshal.SizeOf<ChunkBoundsGPU>());

            Array.Resize(ref _slotToCoord, newMax);

            _maxChunkSlots = newMax;

#if LITHFORGE_DEBUG
            _logger?.LogInfo(
                $"[ChunkMeshStore] Grew slot capacity: {oldMax} → {newMax}");
#endif
        }

        /// <summary>
        ///     Updates the shared chunk bounds buffer for GPU culling at the given cull slot.
        /// </summary>
        private void UpdateChunkBounds(int3 coord, int cullSlotId)
        {
            if (cullSlotId < 0)
            {
                return;
            }

            float3 worldMin = new(
                coord.x * ChunkConstants.Size,
                coord.y * ChunkConstants.Size,
                coord.z * ChunkConstants.Size);
            float3 worldMax = worldMin + new float3(ChunkConstants.Size);

            _boundsUpload[0] = new ChunkBoundsGPU
            {
                WorldMin = worldMin, WorldMax = worldMax,
            };
            _chunkBoundsBuffer.SetData(_boundsUpload, 0, cullSlotId, 1);
        }

        /// <summary>
        ///     Submits procedural indexed draw calls for all render layers using per-chunk
        ///     indirect args with GPU frustum culling. Each arena in each pool gets its own
        ///     draw call. A compute shader tests AABB vs frustum and sets instanceCount=0
        ///     for culled chunks. Must be called from LateUpdate.
        /// </summary>
        public void RenderAll(Camera camera)
        {
            // Batch-upload all dirty ranges across all arenas
            Profiler.BeginSample("CMS.Flush");
            OpaquePool.FlushDirtyToGpu();
            CutoutPool.FlushDirtyToGpu();
            TranslucentPool.FlushDirtyToGpu();
            Profiler.EndSample();

            // GPU culling: frustum + optional Hi-Z occlusion
            // Swap-and-pop keeps active cull slots contiguous in 0..RendererCount-1
            int activeSlotCount = RendererCount;

            if (_frustumCullShader != null && activeSlotCount > 0)
            {
                GeometryUtility.CalculateFrustumPlanes(camera, _frustumPlanes);

                for (int i = 0; i < 6; i++)
                {
                    _frustumPlaneVectors[i] = new Vector4(
                        _frustumPlanes[i].normal.x,
                        _frustumPlanes[i].normal.y,
                        _frustumPlanes[i].normal.z,
                        _frustumPlanes[i].distance);
                }

                _frustumCullShader.SetVectorArray(s_frustumPlanesId, _frustumPlaneVectors);
                _frustumCullShader.SetInt(s_slotCountId, activeSlotCount);
                _frustumCullShader.SetBuffer(_resetCullKernel, s_chunkBoundsBufferId, _chunkBoundsBuffer);

                int threadGroups = (activeSlotCount + 63) / 64;

                // Try Hi-Z occlusion from previous frame's depth
                bool useOcclusion = false;

                if (_hiZPyramid != null)
                {
                    Texture depthTexture = Shader.GetGlobalTexture("_CameraDepthTexture");
                    _hiZPyramid.Generate(depthTexture);
                    useOcclusion = _hiZPyramid.IsValid;
                }

                if (useOcclusion)
                {
                    Matrix4x4 vpMatrix =
                        GL.GetGPUProjectionMatrix(camera.projectionMatrix, true)
                        * camera.worldToCameraMatrix;
                    _frustumCullShader.SetMatrix(s_viewProjMatrixId, vpMatrix);
                    _frustumCullShader.SetInts(s_hiZSizeId, _hiZPyramid.Width, _hiZPyramid.Height);
                    _frustumCullShader.SetInt(s_hiZMipCountId, _hiZPyramid.MipCount);
                    _frustumCullShader.SetBuffer(
                        _occlusionCullKernel, s_chunkBoundsBufferId, _chunkBoundsBuffer);
                    _frustumCullShader.SetTexture(
                        _occlusionCullKernel, s_hiZTextureId, _hiZPyramid.CombinedTexture);

                    DispatchCullForPool(OpaquePool, threadGroups, useOcclusion: true);
                    DispatchCullForPool(CutoutPool, threadGroups, useOcclusion: true);
                    DispatchCullForPool(TranslucentPool, threadGroups, useOcclusion: true);
                }
                else
                {
                    _frustumCullShader.SetBuffer(
                        _frustumCullKernel, s_chunkBoundsBufferId, _chunkBoundsBuffer);

                    DispatchCullForPool(OpaquePool, threadGroups, useOcclusion: false);
                    DispatchCullForPool(CutoutPool, threadGroups, useOcclusion: false);
                    DispatchCullForPool(TranslucentPool, threadGroups, useOcclusion: false);
                }
            }

            Profiler.BeginSample("CMS.Draw");
            DrawPool(OpaquePool, _opaqueParams, activeSlotCount);
            DrawPool(CutoutPool, _cutoutParams, activeSlotCount);
            DrawPool(TranslucentPool, _translucentParams, activeSlotCount);
            Profiler.EndSample();
        }

        /// <summary>
        ///     Dispatches reset-cull and frustum/occlusion-cull compute kernels for all arenas
        ///     in one render layer pool.
        /// </summary>
        private void DispatchCullForPool(BufferArenaPool pool, int threadGroups, bool useOcclusion)
        {
            for (int a = 0; a < pool.ArenaCount; a++)
            {
                ArenaDrawBatch batch = pool.GetDrawBatch(a, RendererCount);

                if (!batch.HasGeometry)
                {
                    continue;
                }

                // Reset: restore instanceCount=1 for all slots with indexCount > 0
                _frustumCullShader.SetBuffer(_resetCullKernel, s_perChunkArgsBufferId, batch.PerChunkArgsBuffer);
                _frustumCullShader.Dispatch(_resetCullKernel, threadGroups, 1, 1);

                if (useOcclusion)
                {
                    _frustumCullShader.SetBuffer(_occlusionCullKernel, s_perChunkArgsBufferId, batch.PerChunkArgsBuffer);
                    _frustumCullShader.Dispatch(_occlusionCullKernel, threadGroups, 1, 1);
                }
                else
                {
                    _frustumCullShader.SetBuffer(_frustumCullKernel, s_perChunkArgsBufferId, batch.PerChunkArgsBuffer);
                    _frustumCullShader.Dispatch(_frustumCullKernel, threadGroups, 1, 1);
                }
            }
        }

        /// <summary>
        ///     Issues one RenderPrimitivesIndexedIndirect draw per arena in the pool.
        /// </summary>
        private void DrawPool(BufferArenaPool pool, RenderParams rp, int commandCount)
        {
            if (commandCount <= 0)
            {
                return;
            }

            for (int a = 0; a < pool.ArenaCount; a++)
            {
                ArenaDrawBatch batch = pool.GetDrawBatch(a, commandCount);

                if (!batch.HasGeometry)
                {
                    continue;
                }

                rp.matProps.SetBuffer(s_vertexBufferId, batch.VertexBuffer);
                Graphics.RenderPrimitivesIndexedIndirect(
                    rp,
                    MeshTopology.Triangles,
                    batch.IndexBuffer,
                    batch.PerChunkArgsBuffer,
                    commandCount);
            }
        }

        /// <summary>
        ///     Frees all mesh data for the given chunk and reclaims its cull slot via swap-and-pop,
        ///     keeping active slots contiguous for efficient compute dispatch.
        /// </summary>
        public void DestroyRenderer(int3 coord)
        {
            OpaquePool.Free(coord);
            CutoutPool.Free(coord);
            TranslucentPool.Free(coord);

            if (_coordToCullSlot.TryGetValue(coord, out int cullSlotId))
            {
                int lastSlot = RendererCount - 1;

                if (cullSlotId != lastSlot)
                {
                    // Swap: move the last active chunk's draw data into the freed slot
                    int3 lastCoord = _slotToCoord[lastSlot];

                    OpaquePool.UpdatePerChunkArgs(cullSlotId, lastCoord);
                    CutoutPool.UpdatePerChunkArgs(cullSlotId, lastCoord);
                    TranslucentPool.UpdatePerChunkArgs(cullSlotId, lastCoord);
                    UpdateChunkBounds(lastCoord, cullSlotId);

                    _coordToCullSlot[lastCoord] = cullSlotId;
                    _slotToCoord[cullSlotId] = lastCoord;
                }

                // Zero the removed slot's args in all arenas
                OpaquePool.ZeroPerChunkArgs(lastSlot);
                CutoutPool.ZeroPerChunkArgs(lastSlot);
                TranslucentPool.ZeroPerChunkArgs(lastSlot);

                RendererCount--;
                _coordToCullSlot.Remove(coord);
            }
        }
    }
}
