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
    [BurstCompile]
    public struct InitialLightingJob : IJob
    {
        [ReadOnly] public NativeArray<int> HeightMap;
        [ReadOnly] public int3 ChunkCoord;
        // ChunkData is written by OreGenerationJob; InitialLightingJob chains after it
        // via JobHandle. Safety restriction disabled because ChunkData NativeArray is
        // aliased across the linear generation pipeline (TerrainShape → Cave → Surface → Ore → here).
        [ReadOnly] [NativeDisableContainerSafetyRestriction] public NativeArray<StateId> ChunkData;
        [ReadOnly] public NativeArray<BlockStateCompact> StateTable;

        public NativeArray<byte> LightData;

        private const byte FullSunlight = 15;
        private const byte NoLight = 0;

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

        private bool IsColumnExposedAbove(int x, int z)
        {
            int columnIndex = z * ChunkConstants.Size + x;
            int surfaceY = HeightMap[columnIndex];
            int chunkTopWorldY = ChunkCoord.y * ChunkConstants.Size + ChunkConstants.Size - 1;

            return chunkTopWorldY >= surfaceY;
        }
    }
}
