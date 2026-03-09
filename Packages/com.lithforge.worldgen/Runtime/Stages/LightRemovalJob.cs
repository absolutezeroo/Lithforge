using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Lithforge.WorldGen.Stages
{
    /// <summary>
    /// Stub for Sprint 4: block-change-triggered light recalculation.
    /// When a block is placed or removed, this job removes stale light values
    /// and re-propagates from affected sources.
    /// </summary>
    [BurstCompile]
    public struct LightRemovalJob : IJob
    {
        public NativeArray<byte> LightData;

        [ReadOnly] public NativeArray<StateId> ChunkData;
        [ReadOnly] public NativeArray<BlockStateCompact> StateTable;
        [ReadOnly] public NativeArray<int> ChangedIndices;

        public void Execute()
        {
            // Stub — Sprint 4 will implement BFS light removal and re-propagation
            // triggered by block placement/removal events.
        }
    }
}
