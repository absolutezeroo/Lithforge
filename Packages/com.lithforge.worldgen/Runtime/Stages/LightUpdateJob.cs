using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.WorldGen.Lighting;

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Lithforge.WorldGen.Stages
{
    /// <summary>
    ///     Lightweight cross-chunk light update job. Seeds border voxels from neighbor
    ///     border light values and propagates only the delta (voxels where incoming light
    ///     exceeds existing light). Does NOT do a full re-propagation.
    ///     Owner of SeedEntries: caller (GenerationScheduler). Dispose: caller after Complete().
    ///     Owner of LightData: ManagedChunk (Persistent). Not disposed by this job.
    ///     Owner of ChunkData: ManagedChunk (via ChunkPool, Persistent). Not disposed by this job.
    ///     Owner of StateTable: NativeStateRegistry (Persistent). Not disposed by this job.
    /// </summary>
    [BurstCompile]
    public struct LightUpdateJob : IJob
    {
        /// <summary>Per-voxel light data modified in-place by seeding and propagation.</summary>
        public NativeArray<byte> LightData;

        /// <summary>Chunk voxel data for opacity lookups.</summary>
        [ReadOnly] public NativeArray<StateId> ChunkData;

        /// <summary>Block state compact table for opacity and filter lookups.</summary>
        [ReadOnly] public NativeArray<BlockStateCompact> StateTable;

        /// <summary>
        ///     Seed entries: border light values from neighboring chunks, mapped to local
        ///     coordinates in this chunk. Each entry's LocalPosition is the position in THIS
        ///     chunk where the neighbor's light should seed.
        ///     Owner: caller. Dispose: caller after Complete().
        /// </summary>
        [ReadOnly] public NativeArray<NativeBorderLightEntry> SeedEntries;

        /// <summary>Seeds border voxels from neighbor light entries, then propagates only the delta.</summary>
        public void Execute()
        {
            NativeQueue<int> sunQueue = new(Allocator.TempJob);
            NativeQueue<int> blockQueue = new(Allocator.TempJob);

            // Seed from neighbor border values
            for (int i = 0; i < SeedEntries.Length; i++)
            {
                NativeBorderLightEntry seed = SeedEntries[i];
                int x = seed.LocalPosition.x;
                int y = seed.LocalPosition.y;
                int z = seed.LocalPosition.z;

                if (x < 0 || x >= ChunkConstants.Size ||
                    y < 0 || y >= ChunkConstants.Size ||
                    z < 0 || z >= ChunkConstants.Size)
                {
                    continue;
                }

                int index = Voxel.Chunk.ChunkData.GetIndex(x, y, z);
                StateId stateId = ChunkData[index];
                BlockStateCompact blockState = StateTable[stateId.Value];

                if (blockState.IsOpaque)
                {
                    continue;
                }

                byte incomingSun = LightUtils.GetSunLight(seed.PackedLight);
                byte incomingBlock = LightUtils.GetBlockLight(seed.PackedLight);

                // Attenuate by 1 (crossing chunk boundary counts as one step)
                byte filter = blockState.LightFilter;
                int sunAttenuation = filter > 0 ? filter : 1;
                int blockAttenuation = filter > 0 ? filter : 1;

                // Special case: sunlight going straight down (face 2 = +Y of neighbor = -Y into this chunk)
                bool isSunDown = seed.Face == 2 && incomingSun == 15;
                int newSun = isSunDown ? 15 : incomingSun - sunAttenuation;
                int newBlock = incomingBlock - blockAttenuation;

                if (newSun < 0) { newSun = 0; }
                if (newBlock < 0) { newBlock = 0; }

                byte currentPacked = LightData[index];
                byte currentSun = LightUtils.GetSunLight(currentPacked);
                byte currentBlock = LightUtils.GetBlockLight(currentPacked);

                bool changed = false;

                if ((byte)newSun > currentSun)
                {
                    currentSun = (byte)newSun;
                    changed = true;
                }

                if ((byte)newBlock > currentBlock)
                {
                    currentBlock = (byte)newBlock;
                    changed = true;
                }

                if (changed)
                {
                    LightData[index] = LightUtils.Pack(currentSun, currentBlock);

                    if (currentSun > 1)
                    {
                        sunQueue.Enqueue(index | currentSun << LightBfs.LevelShift);
                    }

                    if (currentBlock > 1)
                    {
                        blockQueue.Enqueue(index | currentBlock << LightBfs.LevelShift);
                    }
                }
            }

            // Propagate sunlight delta
            LightBfs.PropagateSun(ref sunQueue, ref LightData, ref ChunkData, ref StateTable);

            // Propagate block light delta
            LightBfs.PropagateBlock(ref blockQueue, ref LightData, ref ChunkData, ref StateTable);

            sunQueue.Dispose();
            blockQueue.Dispose();
        }
    }
}
