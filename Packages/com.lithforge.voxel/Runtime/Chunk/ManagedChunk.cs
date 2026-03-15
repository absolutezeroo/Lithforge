using System.Collections.Generic;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.BlockEntity;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lithforge.Voxel.Chunk
{
    /// <summary>
    /// Main-thread bookkeeping wrapper for a single chunk's lifetime.
    /// Holds the NativeArray voxel and light data, lifecycle state, LOD level,
    /// pending edits, border light entries, and block entity storage.
    /// <remarks>
    /// ManagedChunk does NOT own the voxel NativeArray (ChunkPool does).
    /// LightData, HeightMap, and RiverFlags are owned here and disposed by ChunkManager on unload.
    /// </remarks>
    /// </summary>
    public sealed class ManagedChunk
    {
        /// <summary>Chunk coordinate in chunk-space (multiply by 32 for world-space origin).</summary>
        public int3 Coord { get; }

        /// <summary>Current lifecycle state, controlling which schedulers may act on this chunk.</summary>
        public ChunkState State { get; set; }

        /// <summary>Flat voxel storage (32768 StateIds). Checked out from ChunkPool.</summary>
        public NativeArray<StateId> Data { get; set; }

        /// <summary>Nibble-packed light data (high 4 bits = sun, low 4 bits = block). Same length as Data.</summary>
        public NativeArray<byte> LightData { get; set; }

        /// <summary>
        /// Per-column surface heightmap from world generation. Size: ChunkConstants.SizeSquared (1024).
        /// Index: z * ChunkConstants.Size + x. Value: world-space Y of the highest opaque block in column.
        /// Owner: ManagedChunk. Dispose: ChunkManager on unload/shutdown.
        /// </summary>
        public NativeArray<int> HeightMap { get; set; }

        /// <summary>
        /// Per-column river presence flags from RiverNoiseJob. Size: ChunkConstants.SizeSquared (1024).
        /// Index: z * ChunkConstants.Size + x. Value: 0 = no river, 1 = river column.
        /// Used by SurfaceBuilderJob for river bed material and by DecorationStage for tree suppression.
        /// Owner: ManagedChunk. Dispose: ChunkManager on unload/shutdown.
        /// </summary>
        public NativeArray<byte> RiverFlags { get; set; }

        /// <summary>Handle to the currently in-flight Burst job (generation, meshing, or relight).</summary>
        public JobHandle ActiveJobHandle { get; set; }

        /// <summary>
        /// Set when a neighbor changes while this chunk is already meshing,
        /// so MeshScheduler re-queues it after the current mesh job completes.
        /// </summary>
        public bool NeedsRemesh { get; set; }

        /// <summary>
        /// Current LOD level for this chunk. 0 = full detail, 1-3 = downsampled.
        /// </summary>
        public int LODLevel { get; set; }

        /// <summary>
        /// The LOD level that is currently rendered. -1 means no LOD mesh uploaded yet.
        /// Used to detect LOD transitions.
        /// </summary>
        public int RenderedLODLevel { get; set; }

        /// <summary>
        /// Border light entries collected after light propagation.
        /// Contains voxels at chunk borders with light > 1 that need to propagate to neighbors.
        /// Owner: ManagedChunk. Populated by GenerationScheduler after LightPropagationJob completes,
        /// and by MeshScheduler after LightRemovalJob completes.
        /// </summary>
        public List<BorderLightEntry> BorderLightEntries { get; }

        /// <summary>
        /// Flat indices of voxels modified by SetBlock since the last relight.
        /// Used by MeshScheduler to pass targeted changed indices to LightRemovalJob
        /// instead of scanning all 32768 voxels.
        /// Owner: ManagedChunk. Populated by ChunkManager.SetBlock, cleared by MeshScheduler.
        /// </summary>
        public List<int> PendingEditIndices { get; }

        /// <summary>
        /// Border removal seeds from neighboring chunk edits. When a neighbor's relight
        /// reduces light at the shared border, these entries tell this chunk which border
        /// voxels need their light zeroed and removal-BFS'd.
        /// Owner: ManagedChunk. Populated by MeshScheduler after neighbor relight,
        /// cleared by MeshScheduler when scheduling this chunk's LightRemovalJob.
        /// </summary>
        public List<BorderLightEntry> PendingBorderRemovals { get; }

        /// <summary>
        /// Block edits deferred because the chunk was in the Meshing state when SetBlock
        /// was called. Applied to ChunkData after the in-flight mesh job completes, then
        /// the chunk is set to RelightPending for relight + remesh.
        /// Owner: ManagedChunk. Populated by ChunkManager.SetBlock, consumed by
        /// MeshScheduler.PollCompleted after mesh upload.
        /// </summary>
        public List<DeferredEdit> DeferredEdits { get; }

        /// <summary>
        /// True if this chunk needs cross-chunk light re-propagation.
        /// Set when a neighbor's border light values have changed.
        /// </summary>
        public bool NeedsLightUpdate { get; set; }

        /// <summary>
        /// True while a LightUpdateJob is in-flight for this chunk (scheduled last frame,
        /// not yet completed). Guards against double-scheduling and prevents meshing
        /// while light data is being written by a worker thread.
        /// </summary>
        public bool LightJobInFlight { get; set; }

        /// <summary>
        /// When true, the chunk should transition to RelightPending after its current
        /// mesh job completes, instead of to Ready. Set by RelightScheduler when a
        /// border cascade needs to relight a chunk that is currently meshing.
        /// </summary>
        public bool NeedsRelightAfterMesh { get; set; }

        /// <summary>
        /// True if voxel data has been modified (e.g. by SetBlock) since last save.
        /// Used to trigger save-on-unload for modified chunks.
        /// </summary>
        public bool IsDirty { get; set; }

        /// <summary>
        /// Sparse per-block entity storage, keyed by flat voxel index.
        /// Null until a block entity is first placed or loaded in this chunk.
        /// Owner: ManagedChunk. Populated by BlockEntityTickScheduler on placement
        /// or by ChunkSerializer on load. Entities are unloaded via OnChunkUnload().
        /// </summary>
        public Dictionary<int, IBlockEntity> BlockEntities { get; private set; }

        /// <summary>
        /// Lazily creates the BlockEntities dictionary and returns it.
        /// </summary>
        public Dictionary<int, IBlockEntity> GetOrCreateBlockEntities()
        {
            if (BlockEntities == null)
            {
                BlockEntities = new Dictionary<int, IBlockEntity>();
            }

            return BlockEntities;
        }

        public ManagedChunk(int3 coord, NativeArray<StateId> data)
        {
            Coord = coord;
            State = ChunkState.Unloaded;
            Data = data;
            LODLevel = 0;
            RenderedLODLevel = -1;
            BorderLightEntries = new List<BorderLightEntry>();
            PendingEditIndices = new List<int>();
            PendingBorderRemovals = new List<BorderLightEntry>();
            DeferredEdits = new List<DeferredEdit>();
        }
    }
}
