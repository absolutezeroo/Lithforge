using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.WorldGen.Biome;

using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lithforge.WorldGen.Stages
{
    /// <summary>
    /// Replaces surface stone with biome-specific blocks (grass, sand, filler, underwater).
    /// Handles frozen ocean ice patches and river bed material overrides.
    /// Parallelized per XZ column, runs after CaveCarverJob.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Deterministic)]
    public struct SurfaceBuilderJob : IJobParallelFor
    {
        /// <summary>Chunk voxel data modified in-place per column.</summary>
        // ChunkData is aliased across multiple chained jobs via linear JobHandle dependencies.
        [NativeDisableContainerSafetyRestriction, NativeDisableParallelForRestriction]
        public NativeArray<StateId> ChunkData;

        /// <summary>Per-column surface height from TerrainShapeJob.</summary>
        [ReadOnly] public NativeArray<int> HeightMap;

        /// <summary>Per-column dominant biome ID from TerrainShapeJob.</summary>
        [ReadOnly] public NativeArray<byte> BiomeMap;

        /// <summary>Per-biome data for surface block selection.</summary>
        [ReadOnly] public NativeArray<NativeBiomeData> BiomeData;

        /// <summary>Per-column river flags from RiverNoiseJob (0 = no river, 1 = river).</summary>
        [ReadOnly] public NativeArray<byte> RiverFlags;

        /// <summary>Chunk coordinate in chunk-space.</summary>
        [ReadOnly] public int3 ChunkCoord;

        /// <summary>World-space sea level for above/below surface block decisions.</summary>
        [ReadOnly] public int SeaLevel;

        /// <summary>World seed for deterministic ice patch generation.</summary>
        [ReadOnly] public long Seed;

        /// <summary>Stone state to match and replace with surface blocks.</summary>
        [ReadOnly] public StateId StoneId;

        /// <summary>Air state (not replaced by surface builder).</summary>
        [ReadOnly] public StateId AirId;

        /// <summary>Water state for frozen biome ice detection.</summary>
        [ReadOnly] public StateId WaterId;

        /// <summary>Ice state placed on frozen ocean surfaces.</summary>
        [ReadOnly] public StateId IceId;

        /// <summary>Gravel state for non-sand river beds.</summary>
        [ReadOnly] public StateId GravelId;

        /// <summary>Sand state for beach and sand-biome river beds.</summary>
        [ReadOnly] public StateId SandId;

        /// <summary>Applies biome surface blocks, river bed materials, and ice patches for a single XZ column.</summary>
        public void Execute(int columnIndex)
        {
            int x = columnIndex & ChunkConstants.Size - 1;
            int z = columnIndex >> 5;

            int chunkWorldX = ChunkCoord.x * ChunkConstants.Size;
            int chunkWorldY = ChunkCoord.y * ChunkConstants.Size;
            int chunkWorldZ = ChunkCoord.z * ChunkConstants.Size;

            int surfaceY = HeightMap[columnIndex];
            byte biomeId = BiomeMap[columnIndex];
            byte riverFlag = RiverFlags[columnIndex];
            bool isRiver = riverFlag != 0;

            NativeBiomeData biome = GetBiome(biomeId);
            StateId topBlock = biome.TopBlock;
            StateId fillerBlock = biome.FillerBlock;
            StateId underwaterBlock = biome.UnderwaterBlock;
            int fillerDepth = biome.FillerDepth;

            // River bed material override: gravel unless biome surface is sand
            if (isRiver)
            {
                bool isSandBiome = topBlock.Equals(SandId);
                topBlock = isSandBiome ? SandId : GravelId;
                underwaterBlock = isSandBiome ? SandId : GravelId;
            }

            bool isFrozen = (biome.SurfaceFlags & NativeBiomeSurfaceFlags.IsFrozen) != 0;

            for (int y = ChunkConstants.Size - 1; y >= 0; y--)
            {
                int worldY = chunkWorldY + y;
                int index = Voxel.Chunk.ChunkData.GetIndex(x, y, z);
                StateId current = ChunkData[index];

                // Frozen ocean: replace surface water with ice (patchy)
                if (isFrozen && current.Equals(WaterId) && worldY == SeaLevel)
                {
                    uint iceHash = HashColumn(chunkWorldX + x, chunkWorldZ + z, Seed);
                    bool placeIce = iceHash % 10u < 8u;
                    if (placeIce)
                    {
                        ChunkData[index] = IceId;
                    }
                    continue;
                }

                if (!current.Equals(StoneId))
                {
                    continue;
                }

                int depth = surfaceY - worldY;

                if (depth is >= 0 and <= 1)
                {
                    if (surfaceY >= SeaLevel)
                    {
                        ChunkData[index] = topBlock;
                    }
                    else
                    {
                        ChunkData[index] = underwaterBlock;
                    }
                }
                else if (depth > 1 && depth <= fillerDepth + 1)
                {
                    ChunkData[index] = fillerBlock;
                }
            }
        }

        /// <summary>Returns the biome data for the given ID via O(1) direct index lookup.</summary>
        private NativeBiomeData GetBiome(byte biomeId)
        {
            // O(1) direct access: BiomeId is a sequential index assigned in Bootstrap.
            // Invariant: BiomeData[i].BiomeId == i, verified at startup.
            return biomeId < BiomeData.Length ? BiomeData[biomeId] : BiomeData[0];
        }

        /// <summary>Deterministic spatial hash for ice patch distribution.</summary>
        private static uint HashColumn(int x, int z, long seed)
        {
            uint h = (uint)(seed & 0xFFFFFFFF);
            h ^= (uint)x * 374761393u;
            h ^= (uint)z * 668265263u;
            h = (h ^ h >> 13) * 1274126177u;
            h ^= h >> 16;
            return h;
        }
    }
}
