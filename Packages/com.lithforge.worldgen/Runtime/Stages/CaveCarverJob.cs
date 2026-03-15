using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.WorldGen.Noise;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lithforge.WorldGen.Stages
{
    /// <summary>
    /// Carves spaghetti caves by sampling two offset 3D noise fields and replacing
    /// solid blocks with air where n1^2 + n2^2 falls below a depth-adjusted threshold.
    /// Parallelized per XZ column (1024 work items per chunk).
    /// <remarks>
    /// The depth factor makes caves progressively larger underground: at sea level
    /// the threshold is 1.0x, at Y=0 it reaches 1.5x. Water and air blocks are
    /// never carved, and a safety buffer around sea level protects ocean floors.
    /// </remarks>
    /// </summary>
    [BurstCompile]
    public struct CaveCarverJob : IJobParallelFor
    {
        /// <summary>Chunk voxel data to carve into. Written in-place per column.</summary>
        [NativeDisableParallelForRestriction]
        public NativeArray<StateId> ChunkData;

        /// <summary>World seed for deterministic noise generation.</summary>
        [ReadOnly] public long Seed;

        /// <summary>Chunk coordinate in chunk-space (not block-space).</summary>
        [ReadOnly] public int3 ChunkCoord;

        /// <summary>Base noise parameters shared by both cave noise samples.</summary>
        [ReadOnly] public NativeNoiseConfig CaveNoise;

        /// <summary>StateId to write when carving (typically air).</summary>
        [ReadOnly] public StateId AirId;

        /// <summary>StateId for water; these blocks are never carved.</summary>
        [ReadOnly] public StateId WaterId;

        /// <summary>World-space Y of sea level, used for depth factor calculation.</summary>
        [ReadOnly] public int SeaLevel;

        /// <summary>Base threshold for cave detection. Lower values produce fewer caves.</summary>
        [ReadOnly] public float CaveThreshold;

        /// <summary>Minimum world Y below which carving is suppressed.</summary>
        [ReadOnly] public int MinCarveY;

        /// <summary>Seed offset for the first 3D noise sample.</summary>
        [ReadOnly] public int CaveSeedOffset1;

        /// <summary>Seed offset for the second 3D noise sample (must differ from first).</summary>
        [ReadOnly] public int CaveSeedOffset2;

        /// <summary>Vertical buffer in blocks around sea level where carving is forbidden.</summary>
        [ReadOnly] public int SeaLevelCarveBuffer;

        public void Execute(int columnIndex)
        {
            int x = columnIndex & (ChunkConstants.Size - 1);
            int z = columnIndex >> 5;

            int chunkWorldX = ChunkCoord.x * ChunkConstants.Size;
            int chunkWorldY = ChunkCoord.y * ChunkConstants.Size;
            int chunkWorldZ = ChunkCoord.z * ChunkConstants.Size;

            NativeNoiseConfig noise1 = CaveNoise;
            noise1.SeedOffset = CaveSeedOffset1;
            NativeNoiseConfig noise2 = CaveNoise;
            noise2.SeedOffset = CaveSeedOffset2;

            float worldX = chunkWorldX + x;
            float worldZ = chunkWorldZ + z;

            for (int y = 0; y < ChunkConstants.Size; y++)
            {
                int worldY = chunkWorldY + y;

                if (worldY < MinCarveY)
                {
                    continue;
                }

                float n1 = NativeNoise.Sample3D(worldX, worldY, worldZ, noise1, Seed);
                float n2 = NativeNoise.Sample3D(worldX, worldY, worldZ, noise2, Seed);

                float caveValue = n1 * n1 + n2 * n2;

                // Modulate threshold by depth: caves are larger deeper underground.
                // At surface (worldY == SeaLevel), factor is 1.0. At Y=0, factor is 1.5.
                float depthFactor = 1.0f + 0.5f * math.saturate((float)(SeaLevel - worldY) / SeaLevel);
                float adjustedThreshold = CaveThreshold * depthFactor;

                if (caveValue < adjustedThreshold)
                {
                    // Don't carve within buffer of sea level to protect ocean floor
                    if (worldY >= SeaLevel - SeaLevelCarveBuffer && worldY <= SeaLevel)
                    {
                        continue;
                    }

                    int index = Lithforge.Voxel.Chunk.ChunkData.GetIndex(x, y, z);
                    StateId current = ChunkData[index];

                    // Don't carve water or air
                    if (current.Equals(WaterId) || current.Equals(AirId))
                    {
                        continue;
                    }

                    ChunkData[index] = AirId;
                }
            }
        }
    }
}
