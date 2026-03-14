using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Lithforge.WorldGen.Stages
{
    /// <summary>
    /// Applies river carving to the voxel array based on per-column RiverCarveDepth.
    /// For each river column, carves terrain from surfaceY down to (surfaceY - carveDepth).
    /// Blocks at or below sea level become water; blocks above become air.
    ///
    /// Runs after RiverNoiseJob and before CaveCarverJob so cave-river intersections
    /// create natural waterfalls and flooded cave entrances.
    ///
    /// Owner: GenerationPipeline.Schedule allocates RiverCarveDepth (TempJob).
    /// Dispose: RiverCarveDepth disposed in GenerationHandle.Dispose.
    /// </summary>
    [BurstCompile]
    public struct RiverCarveJob : IJob
    {
        // ChunkData is aliased across multiple chained jobs via linear JobHandle dependencies.
        // NativeDisableContainerSafetyRestriction is the established project precedent
        // (see InitialLightingJob) for this safe aliasing pattern.
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<StateId> ChunkData;

        [ReadOnly] public NativeArray<float> RiverCarveDepth;
        [ReadOnly] public NativeArray<int> HeightMap;
        [ReadOnly] public int3 ChunkCoord;
        [ReadOnly] public StateId AirId;
        [ReadOnly] public StateId WaterId;
        [ReadOnly] public StateId StoneId;
        [ReadOnly] public int SeaLevel;

        public void Execute()
        {
            int chunkWorldY = ChunkCoord.y * ChunkConstants.Size;

            for (int z = 0; z < ChunkConstants.Size; z++)
            {
                for (int x = 0; x < ChunkConstants.Size; x++)
                {
                    int columnIndex = z * ChunkConstants.Size + x;
                    float carveDepth = RiverCarveDepth[columnIndex];

                    if (carveDepth < 0.5f)
                    {
                        continue;
                    }

                    int surfaceY = HeightMap[columnIndex];
                    int riverBedY = surfaceY - (int)carveDepth;

                    // Don't carve too far below sea level to avoid punching through to caves
                    int minBedY = SeaLevel - 6;
                    if (riverBedY < minBedY)
                    {
                        riverBedY = minBedY;
                    }

                    // Carve the river channel
                    for (int y = 0; y < ChunkConstants.Size; y++)
                    {
                        int worldY = chunkWorldY + y;

                        // Only carve within the river channel (above bed, at or below surface)
                        if (worldY <= riverBedY || worldY > surfaceY)
                        {
                            continue;
                        }

                        int index = Lithforge.Voxel.Chunk.ChunkData.GetIndex(x, y, z);
                        StateId current = ChunkData[index];

                        // Only carve solid blocks (not air, not already water)
                        if (current.Equals(AirId) || current.Equals(WaterId))
                        {
                            continue;
                        }

                        // Fill with water below sea level, air above
                        if (worldY < SeaLevel)
                        {
                            ChunkData[index] = WaterId;
                        }
                        else
                        {
                            ChunkData[index] = AirId;
                        }
                    }
                }
            }
        }
    }
}
