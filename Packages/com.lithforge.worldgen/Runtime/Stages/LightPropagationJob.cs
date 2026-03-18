using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.WorldGen.Lighting;

using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Lithforge.WorldGen.Stages
{
    [BurstCompile]
    public struct LightPropagationJob : IJob
    {
        [ReadOnly, NativeDisableContainerSafetyRestriction] 
        public NativeArray<StateId> ChunkData;
        [ReadOnly] public NativeArray<BlockStateCompact> StateTable;

        public NativeArray<byte> LightData;

        /// <summary>
        ///     Output: border light entries for cross-chunk propagation.
        ///     Contains voxels at chunk faces with light > 1 that should propagate to neighbors.
        ///     Owner: caller (GenerationScheduler). Dispose: caller after reading.
        /// </summary>
        public NativeList<NativeBorderLightEntry> BorderLightOutput;

        public void Execute()
        {
            NativeQueue<int> sunQueue = new(Allocator.TempJob);
            NativeQueue<int> blockQueue = new(Allocator.TempJob);

            // Seed queues with all voxels that have light > 0.
            // Encode level in the queue entry for stale-entry detection.
            // skipMask bits are 0 (all directions propagated) for initial seeds.
            for (int i = 0; i < ChunkConstants.Volume; i++)
            {
                byte packed = LightData[i];
                byte sun = LightUtils.GetSunLight(packed);
                byte block = LightUtils.GetBlockLight(packed);

                if (sun > 0)
                {
                    sunQueue.Enqueue(i | sun << LightBfs.LevelShift);
                }

                if (block > 0)
                {
                    blockQueue.Enqueue(i | block << LightBfs.LevelShift);
                }
            }

            // Propagate sunlight
            LightBfs.PropagateSun(ref sunQueue, ref LightData, ref ChunkData, ref StateTable);

            // Propagate block light
            LightBfs.PropagateBlock(ref blockQueue, ref LightData, ref ChunkData, ref StateTable);

            sunQueue.Dispose();
            blockQueue.Dispose();

            // Collect border light leaks for cross-chunk propagation
            LightBfs.CollectBorderLightLeaks(ref LightData, ref BorderLightOutput);
        }
    }
}
