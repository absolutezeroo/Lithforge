using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Lithforge.Meshing;
using Lithforge.Voxel.Chunk;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
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
        private static readonly int _vertexBufferId = Shader.PropertyToID("_VertexBuffer");
        private static readonly int _chunkBoundsBufferId = Shader.PropertyToID("_ChunkBoundsBuffer");
        private static readonly int _perChunkArgsBufferId = Shader.PropertyToID("_PerChunkArgsBuffer");
        private static readonly int _frustumPlanesId = Shader.PropertyToID("_FrustumPlanes");
        private static readonly int _slotCountId = Shader.PropertyToID("_SlotCount");

        /// <summary>Very large bounds so URP never frustum-culls the procedural draws.</summary>
        private static readonly Bounds _worldBounds =
            new Bounds(Vector3.zero, new Vector3(100000f, 100000f, 100000f));

        private readonly HashSet<int3> _activeChunks = new HashSet<int3>();
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
        private readonly int _maxChunkSlots;

        /// <summary>
        /// Shared bounds buffer: same AABB regardless of render layer.
        /// Slot IDs are keyed to the opaque buffer's coordToSlotId.
        /// </summary>
        private readonly GraphicsBuffer _chunkBoundsBuffer;
        private readonly ChunkBoundsGPU[] _boundsUpload = new ChunkBoundsGPU[1];

        /// <summary>Cached frustum plane vectors to avoid per-frame array allocation.</summary>
        private readonly Vector4[] _frustumPlaneVectors = new Vector4[6];
        private readonly Plane[] _frustumPlanes = new Plane[6];

        // --- Shared slot ID space (same slot ID for a chunk across all 3 layers) ---
        private readonly Dictionary<int3, int> _coordToSlotId = new Dictionary<int3, int>();
        private readonly Stack<int> _freeSlotIds = new Stack<int>();

        public int RendererCount
        {
            get { return _activeChunks.Count; }
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

        public ChunkMeshStore(
            Material opaqueMaterial, Material cutoutMaterial, Material translucentMaterial,
            int renderDistance, int yLoadMin, int yLoadMax,
            int maxChunkSlots, ComputeShader frustumCullShader)
        {
            OpaqueMaterial = opaqueMaterial;
            CutoutMaterial = cutoutMaterial;
            TranslucentMaterial = translucentMaterial;

            _maxChunkSlots = maxChunkSlots;
            _frustumCullShader = frustumCullShader;

            if (_frustumCullShader != null)
            {
                _resetCullKernel = _frustumCullShader.FindKernel("CSResetCull");
                _frustumCullKernel = _frustumCullShader.FindKernel("CSFrustumCull");
            }

            // Build RenderParams with MaterialPropertyBlock for buffer binding.
            // The vertex buffer is bound per-frame in RenderAll via matProps.
            _opaqueParams = new RenderParams(opaqueMaterial)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = 0,
                worldBounds = _worldBounds,
                matProps = new MaterialPropertyBlock(),
            };
            _cutoutParams = new RenderParams(cutoutMaterial)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = 0,
                worldBounds = _worldBounds,
                matProps = new MaterialPropertyBlock(),
            };
            _translucentParams = new RenderParams(translucentMaterial)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = 0,
                worldBounds = _worldBounds,
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

            // Pre-fill shared free slot IDs (push in reverse so Pop returns 0 first)
            for (int i = maxChunkSlots - 1; i >= 0; i--)
            {
                _freeSlotIds.Push(i);
            }
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
            _activeChunks.Add(coord);

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
            _activeChunks.Add(coord);

            int slotId = EnsureSlotId(coord);
            _opaqueBuffer.UpdatePerChunkArgs(slotId, coord);
            _cutoutBuffer.ZeroPerChunkArgs(slotId);
            _translucentBuffer.ZeroPerChunkArgs(slotId);
            UpdateChunkBounds(coord, slotId);
        }

        /// <summary>
        /// Ensures a shared slot ID exists for the given chunk coordinate.
        /// Allocates one from the free pool if this is a new chunk.
        /// </summary>
        private int EnsureSlotId(int3 coord)
        {
            if (_coordToSlotId.TryGetValue(coord, out int slotId))
            {
                return slotId;
            }

            if (_freeSlotIds.Count == 0)
            {
                UnityEngine.Debug.LogError(
                    $"[ChunkMeshStore] No free per-chunk slots (max={_maxChunkSlots}). " +
                    "Increase ChunkSettings.MaxChunkRenderSlots.");
                return -1;
            }

            slotId = _freeSlotIds.Pop();
            _coordToSlotId[coord] = slotId;
            return slotId;
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
            _opaqueBuffer.FlushArgs();
            _cutoutBuffer.FlushArgs();
            _translucentBuffer.FlushArgs();

            // GPU frustum culling: extract planes, dispatch reset + cull, then draw
            if (_frustumCullShader != null)
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

                _frustumCullShader.SetVectorArray(_frustumPlanesId, _frustumPlaneVectors);
                _frustumCullShader.SetInt(_slotCountId, _maxChunkSlots);
                _frustumCullShader.SetBuffer(_resetCullKernel, _chunkBoundsBufferId, _chunkBoundsBuffer);
                _frustumCullShader.SetBuffer(_frustumCullKernel, _chunkBoundsBufferId, _chunkBoundsBuffer);

                int threadGroups = (_maxChunkSlots + 63) / 64;

                DispatchResetAndCull(_opaqueBuffer, threadGroups);
                DispatchResetAndCull(_cutoutBuffer, threadGroups);
                DispatchResetAndCull(_translucentBuffer, threadGroups);
            }

            DrawLayer(_opaqueBuffer, _opaqueParams);
            DrawLayer(_cutoutBuffer, _cutoutParams);
            DrawLayer(_translucentBuffer, _translucentParams);
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
            _frustumCullShader.SetBuffer(_resetCullKernel, _perChunkArgsBufferId, buffer.PerChunkArgsBuffer);
            _frustumCullShader.Dispatch(_resetCullKernel, threadGroups, 1, 1);

            // Frustum cull: set instanceCount=0 for chunks outside frustum
            _frustumCullShader.SetBuffer(_frustumCullKernel, _perChunkArgsBufferId, buffer.PerChunkArgsBuffer);
            _frustumCullShader.Dispatch(_frustumCullKernel, threadGroups, 1, 1);
        }

        /// <summary>
        /// Issues one RenderPrimitivesIndexedIndirect draw with per-chunk multi-draw.
        /// </summary>
        private void DrawLayer(MegaMeshBuffer buffer, RenderParams rp)
        {
            if (!buffer.HasGeometry)
            {
                return;
            }

            rp.matProps.SetBuffer(_vertexBufferId, buffer.VertexBuffer);
            Graphics.RenderPrimitivesIndexedIndirect(
                rp,
                MeshTopology.Triangles,
                buffer.IndexBuffer,
                buffer.PerChunkArgsBuffer,
                _maxChunkSlots,
                0);
        }

        public void DestroyRenderer(int3 coord)
        {
            _opaqueBuffer.Free(coord);
            _cutoutBuffer.Free(coord);
            _translucentBuffer.Free(coord);
            _activeChunks.Remove(coord);

            if (_coordToSlotId.TryGetValue(coord, out int slotId))
            {
                _opaqueBuffer.ZeroPerChunkArgs(slotId);
                _cutoutBuffer.ZeroPerChunkArgs(slotId);
                _translucentBuffer.ZeroPerChunkArgs(slotId);
                _coordToSlotId.Remove(coord);
                _freeSlotIds.Push(slotId);
            }
        }

        public void Dispose()
        {
            _activeChunks.Clear();
            _coordToSlotId.Clear();
            _freeSlotIds.Clear();
            _opaqueBuffer.Dispose();
            _cutoutBuffer.Dispose();
            _translucentBuffer.Dispose();
            _chunkBoundsBuffer?.Dispose();
        }
    }
}
