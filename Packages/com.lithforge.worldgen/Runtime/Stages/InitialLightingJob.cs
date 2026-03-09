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
        [ReadOnly] [NativeDisableContainerSafetyRestriction] public NativeArray<StateId> ChunkData;
        [ReadOnly] public NativeArray<BlockStateCompact> StateTable;

        public NativeArray<byte> LightData;

        private const byte _fullSunlight = 15;
        private const byte _noLight = 0;

        public void Execute()
        {
            int chunkWorldY = ChunkCoord.y * ChunkConstants.Size;

            for (int z = 0; z < ChunkConstants.Size; z++)
            {
                for (int x = 0; x < ChunkConstants.Size; x++)
                {
                    int columnIndex = z * ChunkConstants.Size + x;
                    int surfaceY = HeightMap[columnIndex];

                    for (int y = ChunkConstants.Size - 1; y >= 0; y--)
                    {
                        int worldY = chunkWorldY + y;
                        int index = Lithforge.Voxel.Chunk.ChunkData.GetIndex(x, y, z);

                        byte sun = _noLight;
                        byte block = _noLight;

                        if (worldY >= surfaceY)
                        {
                            sun = _fullSunlight;
                        }

                        // Seed block light from emitting blocks
                        StateId stateId = ChunkData[index];

                        if (stateId.Value < StateTable.Length)
                        {
                            BlockStateCompact state = StateTable[stateId.Value];

                            if (state.EmitsLight)
                            {
                                block = state.LightEmission;
                            }
                        }

                        LightData[index] = LightUtils.Pack(sun, block);
                    }
                }
            }
        }
    }
}
