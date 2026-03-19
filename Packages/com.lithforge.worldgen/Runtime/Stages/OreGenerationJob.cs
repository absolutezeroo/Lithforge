using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.WorldGen.Ore;

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lithforge.WorldGen.Stages
{
    /// <summary>
    /// Places ore veins (blob or scatter) in stone blocks using deterministic per-ore-per-chunk RNG.
    /// Ores are processed in array order; earlier ores win at intersection points.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Deterministic)]
    public struct OreGenerationJob : IJob
    {
        /// <summary>Chunk voxel data to place ores into. Modified in-place.</summary>
        public NativeArray<StateId> ChunkData;

        /// <summary>Per-ore-type generation configs (Y range, vein size, frequency, type).</summary>
        [ReadOnly] public NativeArray<NativeOreConfig> OreConfigs;

        /// <summary>World seed for deterministic ore RNG.</summary>
        [ReadOnly] public long Seed;

        /// <summary>Chunk coordinate in chunk-space.</summary>
        [ReadOnly] public int3 ChunkCoord;

        /// <summary>Stone state used as the default replacement target for ores.</summary>
        [ReadOnly] public StateId StoneId;

        /// <summary>Generates all ore veins for this chunk, iterating ores in priority order.</summary>
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

        /// <summary>Places a single ore block if the target voxel is stone or the ore's replacement target.</summary>
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

        /// <summary>Places a spheroid ore blob with distance-based probability falloff.</summary>
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
