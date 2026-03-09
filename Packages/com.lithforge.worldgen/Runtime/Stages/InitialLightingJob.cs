using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lithforge.WorldGen.Stages
{
    /// <summary>
    /// Burst-compiled heightmap-based sunlight calculation.
    /// Voxels above the surface height get full sunlight (15),
    /// voxels at or below get no sunlight (0).
    /// Chained after SurfaceBuilderJob in the generation pipeline.
    /// </summary>
    [BurstCompile]
    public struct InitialLightingJob : IJob
    {
        [ReadOnly] public NativeArray<StateId> ChunkData;
        [ReadOnly] public NativeArray<int> HeightMap;
        [ReadOnly] public NativeArray<BlockStateCompact> StateTable;
        [ReadOnly] public int3 ChunkCoord;

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

                        if (worldY > surfaceY)
                        {
                            LightData[index] = _fullSunlight;
                        }
                        else
                        {
                            LightData[index] = _noLight;
                        }
                    }
                }
            }
        }
    }
}
