using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.WorldGen.Ore;

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lithforge.WorldGen.Stages
{
    [BurstCompile]
    public struct OreGenerationJob : IJob
    {
        public NativeArray<StateId> ChunkData;

        [ReadOnly] public NativeArray<NativeOreConfig> OreConfigs;
        [ReadOnly] public long Seed;
        [ReadOnly] public int3 ChunkCoord;
        [ReadOnly] public StateId StoneId;

        public void Execute()
        {
            int chunkWorldX = ChunkCoord.x * ChunkConstants.Size;
            int chunkWorldY = ChunkCoord.y * ChunkConstants.Size;
            int chunkWorldZ = ChunkCoord.z * ChunkConstants.Size;

            // IMPORTANT: Ore processing order is significant.
            // Ores are processed in OreConfigs array order. PlaceOreBlock only replaces
            // StoneId (or the ore's ReplaceStateId), so an ore placed by an earlier index
            // cannot be overwritten by a later ore (the replaced block is no longer stone).
            // At intersection points, the first ore in the array "wins".
            // Order the array from rarest/most-valuable to most-common to preserve rare ores.
            for (int oreIdx = 0; oreIdx < OreConfigs.Length; oreIdx++)
            {
                NativeOreConfig config = OreConfigs[oreIdx];

                // Check if this chunk's Y range overlaps with ore's Y range
                int chunkMinY = chunkWorldY;
                int chunkMaxY = chunkWorldY + ChunkConstants.Size - 1;

                if (chunkMaxY < config.MinY || chunkMinY > config.MaxY)
                {
                    continue;
                }

                // Deterministic seed per ore per chunk
                uint oreSeed = (uint)(Seed ^ oreIdx * 73856093L
                                           ^ ChunkCoord.x * 19349663L
                                           ^ ChunkCoord.y * 83492791L
                                           ^ ChunkCoord.z * 50331653L);

                Random rng = new(math.max(1u, oreSeed));

                int veinCount = (int)config.Frequency;

                for (int v = 0; v < veinCount; v++)
                {
                    int centerX = rng.NextInt(0, ChunkConstants.Size);
                    int centerY = rng.NextInt(0, ChunkConstants.Size);
                    int centerZ = rng.NextInt(0, ChunkConstants.Size);

                    int worldCenterY = chunkWorldY + centerY;

                    if (worldCenterY < config.MinY || worldCenterY > config.MaxY)
                    {
                        continue;
                    }

                    if (config.OreType == 0)
                    {
                        // Scatter: place single block
                        PlaceOreBlock(centerX, centerY, centerZ, config);
                    }
                    else
                    {
                        // Blob: fill spheroid
                        PlaceOreBlob(centerX, centerY, centerZ, config, ref rng);
                    }
                }
            }
        }

        private void PlaceOreBlock(int x, int y, int z, NativeOreConfig config)
        {
            if (x < 0 || x >= ChunkConstants.Size ||
                y < 0 || y >= ChunkConstants.Size ||
                z < 0 || z >= ChunkConstants.Size)
            {
                return;
            }

            int index = Voxel.Chunk.ChunkData.GetIndex(x, y, z);
            StateId current = ChunkData[index];

            if (current.Equals(config.ReplaceStateId) || current.Equals(StoneId))
            {
                ChunkData[index] = config.OreStateId;
            }
        }

        private void PlaceOreBlob(int cx, int cy, int cz, NativeOreConfig config, ref Random rng)
        {
            float radius = math.pow(config.VeinSize * 0.75f, 1.0f / 3.0f);
            int iRadius = (int)math.ceil(radius);

            for (int dz = -iRadius; dz <= iRadius; dz++)
            {
                for (int dy = -iRadius; dy <= iRadius; dy++)
                {
                    for (int dx = -iRadius; dx <= iRadius; dx++)
                    {
                        float dist = math.sqrt(dx * dx + dy * dy + dz * dz);

                        if (dist > radius)
                        {
                            continue;
                        }

                        // Probability decreases with distance from center
                        float chance = 1.0f - dist / (radius + 0.5f);

                        if (rng.NextFloat() > chance)
                        {
                            continue;
                        }

                        PlaceOreBlock(cx + dx, cy + dy, cz + dz, config);
                    }
                }
            }
        }
    }
}
