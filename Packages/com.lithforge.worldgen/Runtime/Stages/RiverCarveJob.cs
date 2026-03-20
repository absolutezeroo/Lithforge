using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

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
    /// Owner: GenerationPipeline.Schedule allocates RiverCarveDepth (Persistent).
    /// Dispose: RiverCarveDepth disposed in GenerationHandle.Dispose.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Deterministic)]
    public struct RiverCarveJob : IJobParallelFor
    {
        /// <summary>Chunk voxel data carved in-place per column.</summary>
        // ChunkData is aliased across multiple chained jobs via linear JobHandle dependencies.
        // NativeDisableContainerSafetyRestriction is the established project precedent
        // (see InitialLightingJob) for this safe aliasing pattern.
        [NativeDisableContainerSafetyRestriction]
        [NativeDisableParallelForRestriction]
        public NativeArray<StateId> ChunkData;

        /// <summary>Per-column carve depth from RiverNoiseJob. Columns with depth less than 0.5 are skipped.</summary>
        [ReadOnly] public NativeArray<float> RiverCarveDepth;

        /// <summary>Per-column surface height from TerrainShapeJob.</summary>
        [ReadOnly] public NativeArray<int> HeightMap;

        /// <summary>Chunk coordinate in chunk-space.</summary>
        [ReadOnly] public int3 ChunkCoord;

        /// <summary>Air block state for carving above sea level.</summary>
        [ReadOnly] public StateId AirId;

        /// <summary>Water block state for carving at or below sea level.</summary>
        [ReadOnly] public StateId WaterId;

        /// <summary>World-space sea level. Carved blocks below become water, above become air.</summary>
        [ReadOnly] public int SeaLevel;

        /// <summary>Carves a single XZ column's river channel from surface down to riverbed.</summary>
        public void Execute(int columnIndex)
        {
            float carveDepth = RiverCarveDepth[columnIndex];

            if (carveDepth < 0.5f)
            {
                return;
            }

            int x = columnIndex & (ChunkConstants.Size - 1);
            int z = columnIndex >> 5;
            int chunkWorldY = ChunkCoord.y * ChunkConstants.Size;
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

                // Fill with water at or below sea level, air above
                if (worldY <= SeaLevel)
                {
                    ChunkData[index] = WaterId;
                }
                else
                {
                    ChunkData[index] = AirId;
                }
            }

            // Fill water in river bed: at minimum the layer just above the carved floor
            // must contain water, even if riverBedY is above sea level.
            int waterTopY = math.min(surfaceY, SeaLevel);
            int waterBotY = riverBedY + 1;

            if (waterBotY > waterTopY)
            {
                // River is entirely above sea level — place exactly 1 water layer
                waterBotY = surfaceY - (int)carveDepth + 1;
                waterTopY = waterBotY;
            }

            for (int wy = waterBotY; wy <= waterTopY; wy++)
            {
                int ly = wy - chunkWorldY;

                if (ly < 0 || ly >= ChunkConstants.Size)
                {
                    continue;
                }

                int idx = Lithforge.Voxel.Chunk.ChunkData.GetIndex(x, ly, z);

                if (!ChunkData[idx].Equals(AirId))
                {
                    continue;
                }

                ChunkData[idx] = WaterId;
            }
        }
    }
}
