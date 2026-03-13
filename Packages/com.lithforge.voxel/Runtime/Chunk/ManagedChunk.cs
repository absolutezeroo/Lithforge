using System.Collections.Generic;
using Lithforge.Voxel.Block;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lithforge.Voxel.Chunk
{
    public sealed class ManagedChunk
    {
        public int3 Coord { get; }

        public ChunkState State { get; set; }

        public NativeArray<StateId> Data { get; set; }

        public NativeArray<byte> LightData { get; set; }

        public JobHandle ActiveJobHandle { get; set; }

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
        /// True if voxel data has been modified (e.g. by SetBlock) since last save.
        /// Used to trigger save-on-unload for modified chunks.
        /// </summary>
        public bool IsDirty { get; set; }

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
        }
    }
}
