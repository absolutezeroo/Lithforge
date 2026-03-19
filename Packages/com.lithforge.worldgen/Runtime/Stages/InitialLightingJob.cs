using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.WorldGen.Lighting;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lithforge.WorldGen.Stages
{
    /// <summary>
    /// Seeds initial sunlight (top-down column scan) and block light (emissive blocks)
    /// into the light data array. Runs after ore generation, before BFS propagation.
    /// </summary>
    [BurstCompile]
    public struct InitialLightingJob : IJob
    {
        /// <summary>Per-column surface height for sun exposure checks.</summary>
        [ReadOnly] public NativeArray<int> HeightMap;

        /// <summary>Chunk coordinate in chunk-space for world-Y calculations.</summary>
        [ReadOnly] public int3 ChunkCoord;

        /// <summary>Chunk voxel data for opacity and emission lookups.</summary>
        // ChunkData is written by OreGenerationJob; InitialLightingJob chains after it
        // via JobHandle. Safety restriction disabled because ChunkData NativeArray is
        // aliased across the linear generation pipeline (TerrainShape → Cave → Surface → Ore → here).
        [ReadOnly] [NativeDisableContainerSafetyRestriction] public NativeArray<StateId> ChunkData;

        /// <summary>Block state compact table for opacity and light emission lookups.</summary>
        [ReadOnly] public NativeArray<BlockStateCompact> StateTable;

        /// <summary>Output light data array, one packed byte per voxel (sun << 4 | block).</summary>
        public NativeArray<byte> LightData;

        /// <summary>Maximum sunlight level (15) for exposed sky columns.</summary>
        private const byte FullSunlight = 15;

        /// <summary>Zero light value for unexposed or opaque voxels.</summary>
        private const byte NoLight = 0;

        /// <summary>Seeds sunlight and block light for all voxels in the chunk.</summary>
        public void Execute()
        {
            for (int z = 0; z < ChunkConstants.Size; z++)
            {
                for (int x = 0; x < ChunkConstants.Size; x++)
                {
                    bool sunExposed = IsColumnExposedAbove(x, z);

                    for (int y = ChunkConstants.Size - 1; y >= 0; y--)
                    {
                        int index = Lithforge.Voxel.Chunk.ChunkData.GetIndex(x, y, z);
                        StateId stateId = ChunkData[index];

                        byte sun = NoLight;
                        byte block = NoLight;

                        if (stateId.Value < StateTable.Length)
                        {
                            BlockStateCompact state = StateTable[stateId.Value];

                            if (sunExposed)
                            {
                                if (state.IsOpaque)
                                {
                                    sunExposed = false;
                                }
                                else
                                {
                                    sun = FullSunlight;
                                }
                            }

                            if (state.EmitsLight)
                            {
                                block = state.LightEmission;
                            }
                        }
                        else if (sunExposed)
                        {
                            sunExposed = false;
                        }

                        LightData[index] = LightUtils.Pack(sun, block);
                    }
                }
            }
        }

        /// <summary>Returns true if the column's top voxel is at or above the surface height.</summary>
        private bool IsColumnExposedAbove(int x, int z)
        {
            int columnIndex = z * ChunkConstants.Size + x;
            int surfaceY = HeightMap[columnIndex];
            int chunkTopWorldY = ChunkCoord.y * ChunkConstants.Size + ChunkConstants.Size - 1;

            return chunkTopWorldY >= surfaceY;
        }
    }
}
