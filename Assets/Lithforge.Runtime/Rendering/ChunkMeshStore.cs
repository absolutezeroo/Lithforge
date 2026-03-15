using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Lithforge.Meshing;
using Lithforge.Voxel.Chunk;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace Lithforge.Runtime.Rendering
{
    /// <summary>
    /// Stores chunk meshes in three persistent GPU buffers (opaque, cutout, translucent)
    /// and draws them via GPU-driven per-chunk indirect draw with compute frustum culling.
    /// Each chunk gets its own DrawIndexedIndirectArgs entry. A compute shader tests AABBs
    /// against the camera frustum and sets instanceCount=0 for culled chunks.
    /// No GameObjects, MeshFilters, MeshRenderers, or Mesh objects are created.
    /// Shaders read vertex data from StructuredBuffer via SV_VertexID.
    /// Owner: LithforgeBootstrap. Lifetime: application session.
    /// </summary>
    public sealed class ChunkMeshStore : IDisposable
    {
        private static readonly int s_vertexBufferId = Shader.PropertyToID("_VertexBuffer");
        private static readonly int s_chunkBoundsBufferId = Shader.PropertyToID("_ChunkBoundsBuffer");
        private static readonly int s_perChunkArgsBufferId = Shader.PropertyToID("_PerChunkArgsBuffer");
        private static readonly int s_frustumPlanesId = Shader.PropertyToID("_FrustumPlanes");
        private static readonly int s_slotCountId = Shader.PropertyToID("_SlotCount");
        private static readonly int s_hiZTextureId = Shader.PropertyToID("_HiZTexture");
        private static readonly int s_viewProjMatrixId = Shader.PropertyToID("_ViewProjMatrix");
        private static readonly int s_hiZSizeId = Shader.PropertyToID("_HiZSize");
        private static readonly int s_hiZMipCountId = Shader.PropertyToID("_HiZMipCount");

        /// <summary>Very large bounds so URP never frustum-culls the procedural draws.</summary>
        private static readonly Bounds s_worldBounds =
            new Bounds(Vector3.zero, new Vector3(100000f, 100000f, 100000f));

        private readonly MegaMeshBuffer _opaqueBuffer;
        private readonly MegaMeshBuffer _cutoutBuffer;
        private readonly MegaMeshBuffer _translucentBuffer;

        private readonly RenderParams _opaqueParams;
        private readonly RenderParams _cutoutParams;
        private readonly RenderParams _translucentParams;

        // --- GPU frustum culling ---
        private readonly ComputeShader _frustumCullShader;
        private readonly int _resetCullKernel;
        private readonly int _frustumCullKernel;
        private int _maxChunkSlots;

        // --- Hi-Z occlusion culling ---
        private readonly HiZPyramid _hiZPyramid;
        private readonly int _occlusionCullKernel;

        /// <summary>
        /// Shared bounds buffer: same AABB regardless of render layer.
        /// Slot IDs are keyed to the opaque buffer's coordToSlotId.
        /// </summary>
        private GraphicsBuffer _chunkBoundsBuffer;
        private readonly ChunkBoundsGPU[] _boundsUpload = new ChunkBoundsGPU[1];

        /// <summary>Cached frustum plane vectors to avoid per-frame array allocation.</summary>
        private readonly Vector4[] _frustumPlaneVectors = new Vector4[6];
        private readonly Plane[] _frustumPlanes = new Plane[6];

        // --- Shared slot ID space (same slot ID for a chunk across all 3 layers) ---
        // Swap-and-pop: active slots are always contiguous in 0.._activeCount-1.
        // When a chunk is destroyed, its slot is swapped with the last active slot.
        private readonly Dictionary<int3, int> _coordToSlotId = new Dictionary<int3, int>();
        private int3[] _slotToCoord;
        private int _activeCount;

        public int RendererCount
        {
            get { return _activeCount; }
        }

        public MegaMeshBuffer OpaqueBuffer
        {
            get { return _opaqueBuffer; }
        }

        public MegaMeshBuffer CutoutBuffer
        {
            get { return _cutoutBuffer; }
        }

        public MegaMeshBuffer TranslucentBuffer
        {
            get { return _translucentBuffer; }
        }

        public Material OpaqueMaterial { get; }

        public Material CutoutMaterial { get; }

        public Material TranslucentMaterial { get; }

        /// <summary>Whether Hi-Z occlusion culling is active (pyramid generated successfully).</summary>
        public bool IsOcclusionCullingActive
        {
            get { return _hiZPyramid != null && _hiZPyramid.IsValid; }
        }

        public ChunkMeshStore(
            Material opaqueMaterial, Material cutoutMaterial, Material translucentMaterial,
            int renderDistance, int yLoadMin, int yLoadMax,
            int yUnloadMin, int yUnloadMax,
            ComputeShader frustumCullShader, ComputeShader hiZGenerateShader)
        {
            OpaqueMaterial = opaqueMaterial;
            CutoutMaterial = cutoutMaterial;
            TranslucentMaterial = translucentMaterial;

            // Compute max chunk slots from unload boundaries (worst-case loaded chunks).
            // Unload radius is renderDistance + 1 on XZ; Y uses the unload range.
            int unloadDiameter = (renderDistance + 1) * 2 + 1;
            int yUnloadLevels = yUnloadMax - yUnloadMin + 1;
            int maxLoadedChunks = unloadDiameter * unloadDiameter * yUnloadLevels;
            int maxChunkSlots = ((maxLoadedChunks + 63) / 64) * 64; // align to compute group size
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
            // The vertex buffer is bound per-frame in RenderAll via matProps.
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

            _opaqueBuffer = new MegaMeshBuffer("MegaMesh_Opaque", opaqueVerts, opaqueIdx, maxChunkSlots);
            _cutoutBuffer = new MegaMeshBuffer("MegaMesh_Cutout", smallVerts, smallIdx, maxChunkSlots);
            _translucentBuffer = new MegaMeshBuffer("MegaMesh_Translucent", smallVerts, smallIdx, maxChunkSlots);

            // Shared chunk bounds buffer — same AABB regardless of render layer.
            // Slot IDs are sourced from the opaque buffer.
            _chunkBoundsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                maxChunkSlots,
                Marshal.SizeOf<ChunkBoundsGPU>());

            // Zero-initialize bounds
            ChunkBoundsGPU[] zeroBounds = new ChunkBoundsGPU[maxChunkSlots];
            _chunkBoundsBuffer.SetData(zeroBounds);

            _slotToCoord = new int3[maxChunkSlots];
        }

        /// <summary>
        /// Updates or creates a chunk's mesh data with 3-submesh data (LOD0).
        /// Called by MeshScheduler.PollCompleted.
        /// </summary>
        public void UpdateRenderer(
            int3 coord,
            NativeList<PackedMeshVertex> opaqueVerts, NativeList<int> opaqueIndices,
            NativeList<PackedMeshVertex> cutoutVerts, NativeList<int> cutoutIndices,
            NativeList<PackedMeshVertex> translucentVerts, NativeList<int> translucentIndices)
        {
            _opaqueBuffer.AllocateOrUpdate(coord, opaqueVerts, opaqueIndices);
            _cutoutBuffer.AllocateOrUpdate(coord, cutoutVerts, cutoutIndices);
            _translucentBuffer.AllocateOrUpdate(coord, translucentVerts, translucentIndices);

            int slotId = EnsureSlotId(coord);
            _opaqueBuffer.UpdatePerChunkArgs(slotId, coord);
            _cutoutBuffer.UpdatePerChunkArgs(slotId, coord);
            _translucentBuffer.UpdatePerChunkArgs(slotId, coord);
            UpdateChunkBounds(coord, slotId);
        }

        /// <summary>
        /// Updates or creates a chunk's mesh data with a single opaque submesh (LOD).
        /// Called by LODScheduler.PollCompleted.
        /// </summary>
        public void UpdateRendererSingleMesh(
            int3 coord,
            NativeList<PackedMeshVertex> vertices, NativeList<int> indices)
        {
            _opaqueBuffer.AllocateOrUpdate(coord, vertices, indices);
            _cutoutBuffer.Free(coord);
            _translucentBuffer.Free(coord);

            int slotId = EnsureSlotId(coord);
            _opaqueBuffer.UpdatePerChunkArgs(slotId, coord);
            _cutoutBuffer.ZeroPerChunkArgs(slotId);
            _translucentBuffer.ZeroPerChunkArgs(slotId);
            UpdateChunkBounds(coord, slotId);
        }

        /// <summary>
        /// Ensures a shared slot ID exists for the given chunk coordinate.
        /// New chunks are appended at _activeCount (contiguous packing).
        /// Grows the slot infrastructure if capacity is exceeded.
        /// </summary>
        private int EnsureSlotId(int3 coord)
        {
            if (_coordToSlotId.TryGetValue(coord, out int slotId))
            {
                return slotId;
            }

            if (_activeCount >= _maxChunkSlots)
            {
                GrowSlotCapacity();
            }

            slotId = _activeCount;
            _slotToCoord[_activeCount] = coord;
            _activeCount++;
            _coordToSlotId[coord] = slotId;
            return slotId;
        }

        /// <summary>
        /// Doubles the slot capacity for per-chunk indirect draw. Grows the shared
        /// bounds buffer and per-chunk args in all three MegaMeshBuffers. Called when
        /// render distance increases at runtime and the original slot count is exceeded.
        /// </summary>
        private void GrowSlotCapacity()
        {
            int oldMax = _maxChunkSlots;
            int newMax = ((oldMax * 2 + 63) / 64) * 64;

            // Grow per-chunk args in all 3 MegaMeshBuffers
            _opaqueBuffer.GrowSlots(newMax);
            _cutoutBuffer.GrowSlots(newMax);
            _translucentBuffer.GrowSlots(newMax);

            // Grow chunk bounds buffer
            GraphicsBuffer newBoundsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                newMax,
                Marshal.SizeOf<ChunkBoundsGPU>());

            ChunkBoundsGPU[] boundsCopy = new ChunkBoundsGPU[newMax];
            _chunkBoundsBuffer.GetData(boundsCopy, 0, 0, oldMax);
            newBoundsBuffer.SetData(boundsCopy);

            _chunkBoundsBuffer.Dispose();
            _chunkBoundsBuffer = newBoundsBuffer;

            Array.Resize(ref _slotToCoord, newMax);

            _maxChunkSlots = newMax;

            UnityEngine.Debug.Log(
                $"[ChunkMeshStore] Grew slot capacity: {oldMax} → {newMax}");
        }

        /// <summary>
        /// Updates the shared chunk bounds buffer for GPU culling at the given slot.
        /// </summary>
        private void UpdateChunkBounds(int3 coord, int slotId)
        {
            if (slotId < 0)
            {
                return;
            }

            float3 worldMin = new float3(
                coord.x * ChunkConstants.Size,
                coord.y * ChunkConstants.Size,
                coord.z * ChunkConstants.Size);
            float3 worldMax = worldMin + new float3(ChunkConstants.Size);

            _boundsUpload[0] = new ChunkBoundsGPU
            {
                WorldMin = worldMin,
                WorldMax = worldMax,
            };
            _chunkBoundsBuffer.SetData(_boundsUpload, 0, slotId, 1);
        }

        /// <summary>
        /// Submits 3 procedural indexed draw calls (opaque, cutout, translucent) using
        /// per-chunk indirect args with GPU frustum culling. Each chunk has its own
        /// DrawIndexedIndirectArgs entry. A compute shader tests AABB vs frustum and
        /// sets instanceCount=0 for culled chunks. Chunks with instanceCount=0 produce
        /// zero GPU work. Must be called from LateUpdate.
        /// </summary>
        public void RenderAll(Camera camera)
        {
            // Batch-upload all dirty ranges (one Lock/Unlock per buffer, not per chunk)
            Profiler.BeginSample("CMS.Flush");
            _opaqueBuffer.FlushDirtyToGpu();
            _cutoutBuffer.FlushDirtyToGpu();
            _translucentBuffer.FlushDirtyToGpu();

            _opaqueBuffer.FlushArgs();
            _cutoutBuffer.FlushArgs();
            _translucentBuffer.FlushArgs();
            Profiler.EndSample();

            // GPU culling: frustum + optional Hi-Z occlusion
            // Swap-and-pop keeps active slots contiguous in 0.._activeCount-1,
            // so commandCount is always exact — no wasted GPU work on empty gaps.
            int activeSlotCount = _activeCount;

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
                    // Set occlusion uniforms (global to compute shader)
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

                    DispatchResetAndOcclusionCull(_opaqueBuffer, threadGroups);
                    DispatchResetAndOcclusionCull(_cutoutBuffer, threadGroups);
                    DispatchResetAndOcclusionCull(_translucentBuffer, threadGroups);
                }
                else
                {
                    _frustumCullShader.SetBuffer(
                        _frustumCullKernel, s_chunkBoundsBufferId, _chunkBoundsBuffer);

                    DispatchResetAndCull(_opaqueBuffer, threadGroups);
                    DispatchResetAndCull(_cutoutBuffer, threadGroups);
                    DispatchResetAndCull(_translucentBuffer, threadGroups);
                }
            }

            Profiler.BeginSample("CMS.Draw");
            DrawLayer(_opaqueBuffer, _opaqueParams, activeSlotCount);
            DrawLayer(_cutoutBuffer, _cutoutParams, activeSlotCount);
            DrawLayer(_translucentBuffer, _translucentParams, activeSlotCount);
            Profiler.EndSample();
        }

        /// <summary>
        /// Dispatches the reset-cull and frustum-cull compute kernels for one render layer.
        /// </summary>
        private void DispatchResetAndCull(MegaMeshBuffer buffer, int threadGroups)
        {
            if (!buffer.HasGeometry)
            {
                return;
            }

            // Reset: restore instanceCount=1 for all slots with indexCount > 0
            _frustumCullShader.SetBuffer(_resetCullKernel, s_perChunkArgsBufferId, buffer.PerChunkArgsBuffer);
            _frustumCullShader.Dispatch(_resetCullKernel, threadGroups, 1, 1);

            // Frustum cull: set instanceCount=0 for chunks outside frustum
            _frustumCullShader.SetBuffer(_frustumCullKernel, s_perChunkArgsBufferId, buffer.PerChunkArgsBuffer);
            _frustumCullShader.Dispatch(_frustumCullKernel, threadGroups, 1, 1);
        }

        /// <summary>
        /// Dispatches the reset-cull and combined frustum+occlusion compute kernels for one layer.
        /// </summary>
        private void DispatchResetAndOcclusionCull(MegaMeshBuffer buffer, int threadGroups)
        {
            if (!buffer.HasGeometry)
            {
                return;
            }

            // Reset: restore instanceCount=1 for all slots with indexCount > 0
            _frustumCullShader.SetBuffer(_resetCullKernel, s_perChunkArgsBufferId, buffer.PerChunkArgsBuffer);
            _frustumCullShader.Dispatch(_resetCullKernel, threadGroups, 1, 1);

            // Occlusion cull: combined frustum + Hi-Z test
            _frustumCullShader.SetBuffer(_occlusionCullKernel, s_perChunkArgsBufferId, buffer.PerChunkArgsBuffer);
            _frustumCullShader.Dispatch(_occlusionCullKernel, threadGroups, 1, 1);
        }

        /// <summary>
        /// Issues one RenderPrimitivesIndexedIndirect draw with per-chunk multi-draw.
        /// </summary>
        private void DrawLayer(MegaMeshBuffer buffer, RenderParams rp, int commandCount)
        {
            if (!buffer.HasGeometry || commandCount <= 0)
            {
                return;
            }

            rp.matProps.SetBuffer(s_vertexBufferId, buffer.VertexBuffer);
            Graphics.RenderPrimitivesIndexedIndirect(
                rp,
                MeshTopology.Triangles,
                buffer.IndexBuffer,
                buffer.PerChunkArgsBuffer,
                commandCount,
                0);
        }

        public void DestroyRenderer(int3 coord)
        {
            _opaqueBuffer.Free(coord);
            _cutoutBuffer.Free(coord);
            _translucentBuffer.Free(coord);

            if (_coordToSlotId.TryGetValue(coord, out int slotId))
            {
                int lastSlot = _activeCount - 1;

                if (slotId != lastSlot)
                {
                    // Swap: move the last active chunk's draw data into the freed slot
                    int3 lastCoord = _slotToCoord[lastSlot];

                    _opaqueBuffer.UpdatePerChunkArgs(slotId, lastCoord);
                    _cutoutBuffer.UpdatePerChunkArgs(slotId, lastCoord);
                    _translucentBuffer.UpdatePerChunkArgs(slotId, lastCoord);
                    UpdateChunkBounds(lastCoord, slotId);

                    _coordToSlotId[lastCoord] = slotId;
                    _slotToCoord[slotId] = lastCoord;
                }

                _activeCount--;
                _coordToSlotId.Remove(coord);
            }
        }

        public void Dispose()
        {
            _hiZPyramid?.Dispose();
            _coordToSlotId.Clear();
            _slotToCoord = null;
            _activeCount = 0;
            _opaqueBuffer.Dispose();
            _cutoutBuffer.Dispose();
            _translucentBuffer.Dispose();
            _chunkBoundsBuffer?.Dispose();
        }
    }
}
