using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Lithforge.Meshing
{
    /// <summary>
    /// Burst-compiled job that downsamples a 32³ chunk to a smaller grid.
    /// LOD1: Scale=2 → 16³, LOD2: Scale=4 → 8³, LOD3: Scale=8 → 4³.
    /// For each downsampled cell, picks the most common opaque non-air block.
    /// If the majority of source voxels are air, the output is air.
    /// </summary>
    [BurstCompile]
    public struct VoxelDownsampleJob : IJob
    {
        [ReadOnly] public NativeArray<StateId> SourceData;
        [ReadOnly] public NativeArray<BlockStateCompact> StateTable;
        public int Scale;

        public NativeArray<StateId> OutputData;

        public void Execute()
        {
            int outSize = ChunkConstants.Size / Scale;

            for (int oz = 0; oz < outSize; oz++)
            {
                for (int oy = 0; oy < outSize; oy++)
                {
                    for (int ox = 0; ox < outSize; ox++)
                    {
                        StateId result = DownsampleCell(ox * Scale, oy * Scale, oz * Scale);
                        int outIndex = oy * outSize * outSize + oz * outSize + ox;
                        OutputData[outIndex] = result;
                    }
                }
            }
        }

        private StateId DownsampleCell(int baseX, int baseY, int baseZ)
        {
            int airCount = 0;
            int total = Scale * Scale * Scale;
            StateId firstOpaque = StateId.Air;
            StateId firstNonAir = StateId.Air;

            for (int dy = 0; dy < Scale; dy++)
            {
                for (int dz = 0; dz < Scale; dz++)
                {
                    for (int dx = 0; dx < Scale; dx++)
                    {
                        int idx = ChunkData.GetIndex(baseX + dx, baseY + dy, baseZ + dz);
                        StateId state = SourceData[idx];

                        if (state.Value == 0)
                        {
                            airCount++;

                            continue;
                        }

                        if (firstNonAir.Value == 0)
                        {
                            firstNonAir = state;
                        }

                        if (firstOpaque.Value == 0 && StateTable[state.Value].IsOpaque)
                        {
                            firstOpaque = state;
                        }
                    }
                }
            }

            // If more than half is air, output air
            if (airCount > total / 2)
            {
                return StateId.Air;
            }

            // Prefer opaque blocks for visual consistency at distance
            if (firstOpaque.Value != 0)
            {
                return firstOpaque;
            }

            return firstNonAir;
        }
    }
}
