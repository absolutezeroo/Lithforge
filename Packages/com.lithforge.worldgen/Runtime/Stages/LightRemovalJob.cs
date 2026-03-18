using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.WorldGen.Lighting;

using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Lithforge.WorldGen.Stages
{
    /// <summary>
    ///     BFS light removal job for block-change-triggered and cross-chunk light recalculation.
    ///     When a block is placed or removed, this job:
    ///     1. Seeds removal queues from border removal seeds (cross-chunk cascade) and
    ///     changed positions (block edits).
    ///     2. Removes stale light values via BFS, with a sunlight column special case
    ///     (downward at max light forces removal regardless of neighbor level).
    ///     3. Collects "re-seed" points (voxels where removed light meets still-valid light).
    ///     4. Re-propagates light from the re-seed points.
    ///     5. Scans chunk borders into BorderLightOutput for cross-chunk cascade.
    ///     Owner of ChangedIndices: caller. Dispose: caller after Complete().
    ///     Owner of BorderRemovalSeeds: caller. Dispose: caller after Complete().
    ///     Owner of BorderLightOutput: caller. Dispose: caller after Complete().
    ///     Owner of LightData: ManagedChunk (Persistent). Not disposed by this job.
    ///     Owner of ChunkData: ManagedChunk (via ChunkPool, Persistent). Not disposed by this job.
    ///     Owner of StateTable: NativeStateRegistry (Persistent). Not disposed by this job.
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    public struct LightRemovalJob : IJob
    {
        public NativeArray<byte> LightData;

        [ReadOnly] public NativeArray<StateId> ChunkData;
        [ReadOnly] public NativeArray<BlockStateCompact> StateTable;

        /// <summary>
        ///     Per-column surface heightmap from world generation. Used for sunlight column
        ///     restoration at chunk-top (y=31) where checking the above voxel is impossible.
        ///     Index: z * ChunkConstants.Size + x. Value: world-space Y of highest opaque block.
        ///     Uses NativeDisableContainerSafetyRestriction because the job may write HeightMap
        ///     when blocks are placed/removed (one job per chunk, gated by LightJobInFlight).
        ///     Owner: ManagedChunk. Not disposed by this job.
        /// </summary>
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<int> HeightMap;

        /// <summary>
        ///     World-space Y coordinate of the chunk's bottom (chunkCoord.y * ChunkConstants.Size).
        ///     Used to convert local Y to world Y for HeightMap comparison.
        /// </summary>
        [ReadOnly] public int ChunkWorldY;

        /// <summary>
        ///     Flat indices of changed voxels that triggered the relight.
        ///     Uses NativeDisableContainerSafetyRestriction because a single persistent
        ///     array may be shared across concurrent LightRemovalJob instances (each
        ///     reading the same full-chunk index list). This is safe because the field
        ///     is read-only and all jobs read identical data.
        ///     Owner: caller. Dispose: caller after Complete().
        /// </summary>
        [ReadOnly, NativeDisableContainerSafetyRestriction]
        public NativeArray<int> ChangedIndices;

        /// <summary>
        ///     Border removal seeds from neighboring chunk edits. Each entry specifies a
        ///     border voxel whose incoming cross-chunk light has decreased and needs removal.
        ///     The LocalPosition is in THIS chunk's coordinate space.
        ///     Owner: caller. Dispose: caller after Complete().
        /// </summary>
        [ReadOnly]
        public NativeArray<NativeBorderLightEntry> BorderRemovalSeeds;

        /// <summary>
        ///     Output: border light entries after removal+reseed completes.
        ///     Contains voxels at chunk faces with light > 1 for cross-chunk cascade.
        ///     The caller compares these with the chunk's old border entries to detect
        ///     changes that need to propagate to further neighbors.
        ///     Owner: caller. Dispose: caller after reading.
        /// </summary>
        public NativeList<NativeBorderLightEntry> BorderLightOutput;

        public void Execute()
        {
            // Queue entries use unified 25-bit encoding:
            //   [24..19: skipMask] [18..15: level] [14..0: index]
            NativeQueue<int> sunRemovalQueue = new(Allocator.TempJob);
            NativeQueue<int> blockRemovalQueue = new(Allocator.TempJob);
            NativeQueue<int> sunReseedQueue = new(Allocator.TempJob);
            NativeQueue<int> blockReseedQueue = new(Allocator.TempJob);

            // Step 1a: Seed removal queues from border removal seeds (cross-chunk cascade)
            SeedFromBorderRemovals(ref sunRemovalQueue, ref blockRemovalQueue,
                ref sunReseedQueue, ref blockReseedQueue);

            // Step 1b: Seed removal queues from changed positions (block edits)
            SeedFromChangedIndices(ref sunRemovalQueue, ref blockRemovalQueue,
                ref sunReseedQueue, ref blockReseedQueue);

            // Step 2: BFS removal of sunlight (with downward column special case)
            RemoveLight(ref sunRemovalQueue, ref sunReseedQueue, true);

            // Step 3: BFS removal of block light
            RemoveLight(ref blockRemovalQueue, ref blockReseedQueue, false);

            // Step 4: Re-propagate from re-seed points
            LightBfs.PropagateSun(ref sunReseedQueue, ref LightData, ref ChunkData, ref StateTable);
            LightBfs.PropagateBlock(ref blockReseedQueue, ref LightData, ref ChunkData, ref StateTable);

            // Step 5: Scan borders for cross-chunk cascade
            LightBfs.CollectBorderLightLeaks(ref LightData, ref BorderLightOutput);

            sunRemovalQueue.Dispose();
            blockRemovalQueue.Dispose();
            sunReseedQueue.Dispose();
            blockReseedQueue.Dispose();
        }

        private void SeedFromBorderRemovals(
            ref NativeQueue<int> sunRemovalQueue, ref NativeQueue<int> blockRemovalQueue,
            ref NativeQueue<int> sunReseedQueue, ref NativeQueue<int> blockReseedQueue)
        {
            for (int i = 0; i < BorderRemovalSeeds.Length; i++)
            {
                NativeBorderLightEntry seed = BorderRemovalSeeds[i];
                int px = seed.LocalPosition.x;
                int py = seed.LocalPosition.y;
                int pz = seed.LocalPosition.z;

                if (px < 0 || px >= ChunkConstants.Size ||
                    py < 0 || py >= ChunkConstants.Size ||
                    pz < 0 || pz >= ChunkConstants.Size)
                {
                    continue;
                }

                int index = Voxel.Chunk.ChunkData.GetIndex(px, py, pz);
                StateId stateId = ChunkData[index];
                BlockStateCompact blockState = StateTable[stateId.Value];

                if (blockState.IsOpaque)
                {
                    continue;
                }

                byte currentPacked = LightData[index];
                byte currentSun = LightUtils.GetSunLight(currentPacked);
                byte currentBlock = LightUtils.GetBlockLight(currentPacked);

                // Zero incoming cross-chunk light for removal BFS
                if (currentSun > 0)
                {
                    LightData[index] = LightUtils.Pack(0, LightUtils.GetBlockLight(LightData[index]));

                    bool borderColumnWrite = false;

                    // Column-write shortcut: if this border voxel is in a sunlight column
                    // (sun=15 at/above heightmap), directly zero the column below and
                    // collect horizontal reseeds instead of letting the removal BFS
                    // cascade down the entire column via the sun=15 downward special case.
                    if (currentSun == 15 && HeightMap.IsCreated)
                    {
                        int colIdx = pz * ChunkConstants.Size + px;
                        int worldY = ChunkWorldY + py;

                        if (worldY >= HeightMap[colIdx])
                        {
                            borderColumnWrite = true;

                            int minLocalY = HeightMap[colIdx] - ChunkWorldY;

                            if (minLocalY < 0)
                            {
                                minLocalY = 0;
                            }

                            // Phase 1: Direct column zero
                            for (int scanY = py - 1; scanY >= minLocalY; scanY--)
                            {
                                int scanIdx = Voxel.Chunk.ChunkData.GetIndex(
                                    px, scanY, pz);

                                if (StateTable[ChunkData[scanIdx].Value].IsOpaque)
                                {
                                    break;
                                }

                                byte scanSun = LightUtils.GetSunLight(LightData[scanIdx]);

                                if (scanSun == 0)
                                {
                                    break;
                                }

                                byte scanBlock = LightUtils.GetBlockLight(LightData[scanIdx]);
                                LightData[scanIdx] = LightUtils.Pack(0, scanBlock);
                            }

                            // Phase 2: Horizontal reseeds for the zeroed column
                            for (int scanY = py; scanY >= minLocalY; scanY--)
                            {
                                int scanIdx = Voxel.Chunk.ChunkData.GetIndex(
                                    px, scanY, pz);

                                if (scanY < py &&
                                    StateTable[ChunkData[scanIdx].Value].IsOpaque)
                                {
                                    break;
                                }

                                TryReseedHorizontalNeighbor(
                                    px - 1, scanY, pz, ref sunReseedQueue);
                                TryReseedHorizontalNeighbor(
                                    px + 1, scanY, pz, ref sunReseedQueue);
                                TryReseedHorizontalNeighbor(
                                    px, scanY, pz - 1, ref sunReseedQueue);
                                TryReseedHorizontalNeighbor(
                                    px, scanY, pz + 1, ref sunReseedQueue);
                            }

                            // Seed the border voxel itself with horizontal-only skip
                            int seedSkip = 1 << LightBfs.DirNegY | 1 << LightBfs.DirPosY;
                            sunRemovalQueue.Enqueue(index | currentSun << LightBfs.LevelShift
                                                          | seedSkip << LightBfs.SkipShift);
                        }
                    }

                    if (!borderColumnWrite)
                    {
                        sunRemovalQueue.Enqueue(index | currentSun << LightBfs.LevelShift);
                    }
                }

                if (currentBlock > 0 && blockState.LightEmission == 0)
                {
                    LightData[index] = LightUtils.Pack(LightUtils.GetSunLight(LightData[index]), 0);
                    blockRemovalQueue.Enqueue(index | currentBlock << LightBfs.LevelShift);
                }

                // If this block emits light, its emission survives as a reseed
                if (blockState.LightEmission > 0)
                {
                    byte updatedBlock = LightUtils.GetBlockLight(LightData[index]);

                    if (blockState.LightEmission > updatedBlock)
                    {
                        byte updatedSun = LightUtils.GetSunLight(LightData[index]);
                        LightData[index] = LightUtils.Pack(updatedSun, blockState.LightEmission);
                    }

                    byte reseedLevel = LightUtils.GetBlockLight(LightData[index]);
                    blockReseedQueue.Enqueue(index | reseedLevel << LightBfs.LevelShift);
                }
            }
        }

        private void SeedFromChangedIndices(
            ref NativeQueue<int> sunRemovalQueue, ref NativeQueue<int> blockRemovalQueue,
            ref NativeQueue<int> sunReseedQueue, ref NativeQueue<int> blockReseedQueue)
        {
            for (int i = 0; i < ChangedIndices.Length; i++)
            {
                int index = ChangedIndices[i];
                byte packed = LightData[index];
                byte sun = LightUtils.GetSunLight(packed);
                byte block = LightUtils.GetBlockLight(packed);

                StateId stateId = ChunkData[index];
                BlockStateCompact blockState = StateTable[stateId.Value];

                // If the new block is opaque, remove all light at this position
                if (blockState.IsOpaque)
                {
                    LightData[index] = LightUtils.Pack(0, 0);

                    bool columnWriteDone = false;

                    // Column-write shortcut for sunlight: directly zero the column below
                    // and collect horizontal border reseeds. No removal BFS needed for the
                    // column itself — the direct write handles it.
                    if (sun > 0 && HeightMap.IsCreated)
                    {
                        LightBfs.IndexToXYZ(index, out int hx, out int hy, out int hz);
                        int colIdx = hz * ChunkConstants.Size + hx;
                        int blockWorldY = ChunkWorldY + hy;
                        int oldHeight = HeightMap[colIdx];

                        if (blockWorldY >= oldHeight)
                        {
                            HeightMap[colIdx] = blockWorldY;
                            columnWriteDone = true;

                            // Phase 1: Direct column zero — write sun=0 for all air below
                            // the placed block down to the old height (or first opaque).
                            // No queue entries needed.
                            int minLocalY = oldHeight - ChunkWorldY;

                            if (minLocalY < 0)
                            {
                                minLocalY = 0;
                            }

                            for (int scanY = hy - 1; scanY >= minLocalY; scanY--)
                            {
                                int scanIdx = Voxel.Chunk.ChunkData.GetIndex(hx, scanY, hz);

                                if (StateTable[ChunkData[scanIdx].Value].IsOpaque)
                                {
                                    break;
                                }

                                byte scanSun = LightUtils.GetSunLight(LightData[scanIdx]);

                                if (scanSun == 0)
                                {
                                    break;
                                }

                                byte scanBlock = LightUtils.GetBlockLight(LightData[scanIdx]);
                                LightData[scanIdx] = LightUtils.Pack(0, scanBlock);
                            }

                            // Phase 2: Collect horizontal border reseeds. For each zeroed
                            // voxel in the column, check its 4 horizontal neighbors. If the
                            // neighbor has sun > 0 and is not opaque, it's still valid light
                            // from another source — reseed it. This replaces the expensive
                            // removal BFS with a direct neighbor scan.
                            for (int scanY = hy; scanY >= minLocalY; scanY--)
                            {
                                int scanIdx = Voxel.Chunk.ChunkData.GetIndex(hx, scanY, hz);

                                // Stop at opaque or at a voxel we didn't zero
                                if (scanY < hy)
                                {
                                    if (StateTable[ChunkData[scanIdx].Value].IsOpaque)
                                    {
                                        break;
                                    }
                                }

                                // Check 4 horizontal neighbors for reseed
                                TryReseedHorizontalNeighbor(hx - 1, scanY, hz, ref sunReseedQueue);
                                TryReseedHorizontalNeighbor(hx + 1, scanY, hz, ref sunReseedQueue);
                                TryReseedHorizontalNeighbor(hx, scanY, hz - 1, ref sunReseedQueue);
                                TryReseedHorizontalNeighbor(hx, scanY, hz + 1, ref sunReseedQueue);
                            }

                            // Check above the placed block — it might need reseed if it
                            // had light from horizontal neighbors
                            if (hy + 1 < ChunkConstants.Size)
                            {
                                int aboveIdx = Voxel.Chunk.ChunkData.GetIndex(hx, hy + 1, hz);
                                byte aboveSun = LightUtils.GetSunLight(LightData[aboveIdx]);

                                if (aboveSun > 0)
                                {
                                    sunReseedQueue.Enqueue(aboveIdx | aboveSun << LightBfs.LevelShift);
                                }
                            }

                            // The placed block itself: seed for horizontal-only removal BFS
                            // with skip ±Y to avoid vertical cascade. This handles horizontal
                            // light that was coming FROM this block's position into neighbors.
                            int seedSkip = 1 << LightBfs.DirNegY | 1 << LightBfs.DirPosY;
                            sunRemovalQueue.Enqueue(index | sun << LightBfs.LevelShift
                                                          | seedSkip << LightBfs.SkipShift);
                        }
                    }

                    // Non-column-write path: standard removal seed
                    if (!columnWriteDone && sun > 0)
                    {
                        sunRemovalQueue.Enqueue(index | sun << LightBfs.LevelShift);
                    }

                    if (block > 0)
                    {
                        blockRemovalQueue.Enqueue(index | block << LightBfs.LevelShift);
                    }

                    // Heightmap update for non-column-write case (block below surface)
                    if (!columnWriteDone && HeightMap.IsCreated)
                    {
                        LightBfs.IndexToXYZ(index, out int hx2, out int hy2, out int hz2);
                        int colIdx2 = hz2 * ChunkConstants.Size + hx2;
                        int blockWorldY2 = ChunkWorldY + hy2;

                        if (blockWorldY2 > HeightMap[colIdx2])
                        {
                            HeightMap[colIdx2] = blockWorldY2;
                        }
                    }
                }
                else
                {
                    LightBfs.IndexToXYZ(index, out int ax, out int ay, out int az);
                    bool columnWriteApplied = false;

                    // Column-write shortcut: when breaking a block at/above the heightmap
                    // surface, directly restore sun=15 in the entire exposed column instead
                    // of a single-voxel reseed. Each restored voxel is enqueued with a
                    // horizontal-only skip mask so the BFS only spreads light sideways.
                    if (HeightMap.IsCreated)
                    {
                        int colIdx = az * ChunkConstants.Size + ax;
                        int worldY = ChunkWorldY + ay;

                        if (worldY >= HeightMap[colIdx])
                        {
                            columnWriteApplied = true;

                            // Scan downward to find the new highest opaque block
                            int newSurface = ChunkWorldY - 1;

                            for (int scanY = ay - 1; scanY >= 0; scanY--)
                            {
                                int scanIdx = Voxel.Chunk.ChunkData.GetIndex(ax, scanY, az);

                                if (StateTable[ChunkData[scanIdx].Value].IsOpaque)
                                {
                                    newSurface = ChunkWorldY + scanY;

                                    break;
                                }
                            }

                            HeightMap[colIdx] = newSurface;

                            // Restore sun=15 from new surface up to broken block position
                            int horizOnlySkip = 1 << LightBfs.DirNegY | 1 << LightBfs.DirPosY;
                            int minLocalY = newSurface - ChunkWorldY;

                            if (minLocalY < 0)
                            {
                                minLocalY = 0;
                            }

                            int maxLocalY = ay;

                            if (maxLocalY >= ChunkConstants.Size)
                            {
                                maxLocalY = ChunkConstants.Size - 1;
                            }

                            for (int scanY = minLocalY; scanY <= maxLocalY; scanY++)
                            {
                                int scanIdx = Voxel.Chunk.ChunkData.GetIndex(ax, scanY, az);

                                if (StateTable[ChunkData[scanIdx].Value].IsOpaque)
                                {
                                    continue;
                                }

                                byte scanBlockLight = LightUtils.GetBlockLight(LightData[scanIdx]);
                                LightData[scanIdx] = LightUtils.Pack(15, scanBlockLight);
                                sunReseedQueue.Enqueue(scanIdx | 15 << LightBfs.LevelShift
                                                               | horizOnlySkip << LightBfs.SkipShift);
                            }
                        }
                    }

                    if (!columnWriteApplied)
                    {
                        // Standard path: single-voxel sun handling for below-surface edits
                        // or chunks without a heightmap.
                        bool sunColumnRestore = false;

                        int columnIndex = az * ChunkConstants.Size + ax;
                        int worldY = ChunkWorldY + ay;

                        if (HeightMap.IsCreated && HeightMap.Length > columnIndex)
                        {
                            if (worldY >= HeightMap[columnIndex])
                            {
                                sunColumnRestore = true;
                            }
                        }
                        else if (ay < ChunkConstants.Size - 1)
                        {
                            // Fallback: check voxel above (no heightmap available)
                            int aboveIndex = Voxel.Chunk.ChunkData.GetIndex(ax, ay + 1, az);
                            byte aboveSun = LightUtils.GetSunLight(LightData[aboveIndex]);

                            if (aboveSun == 15)
                            {
                                sunColumnRestore = true;
                            }
                        }

                        if (sunColumnRestore)
                        {
                            byte currentBlockLight = LightUtils.GetBlockLight(LightData[index]);
                            LightData[index] = LightUtils.Pack(15, currentBlockLight);
                            sunReseedQueue.Enqueue(index | 15 << LightBfs.LevelShift);
                        }
                        else if (sun > 0)
                        {
                            LightData[index] = LightUtils.Pack(0, LightUtils.GetBlockLight(LightData[index]));
                            sunRemovalQueue.Enqueue(index | sun << LightBfs.LevelShift);
                        }

                        // Lower heightmap if removing a block at or above the surface
                        if (HeightMap.IsCreated)
                        {
                            int colIdx = az * ChunkConstants.Size + ax;
                            int blockWorldY = ChunkWorldY + ay;

                            if (blockWorldY >= HeightMap[colIdx])
                            {
                                int newSurface = ChunkWorldY - 1;

                                for (int scanY = ay - 1; scanY >= 0; scanY--)
                                {
                                    int scanIdx = Voxel.Chunk.ChunkData.GetIndex(ax, scanY, az);

                                    if (StateTable[ChunkData[scanIdx].Value].IsOpaque)
                                    {
                                        newSurface = ChunkWorldY + scanY;

                                        break;
                                    }
                                }

                                HeightMap[colIdx] = newSurface;
                            }
                        }
                    }

                    // Block light removal — always applies regardless of column-write path
                    if (block > 0 && blockState.LightEmission == 0)
                    {
                        LightData[index] = LightUtils.Pack(LightUtils.GetSunLight(LightData[index]), 0);
                        blockRemovalQueue.Enqueue(index | block << LightBfs.LevelShift);
                    }

                    // Light emitter re-seeding — always applies
                    if (blockState.LightEmission > 0)
                    {
                        byte currentBlock = LightUtils.GetBlockLight(LightData[index]);

                        if (blockState.LightEmission > currentBlock)
                        {
                            byte currentSun = LightUtils.GetSunLight(LightData[index]);
                            LightData[index] = LightUtils.Pack(currentSun, blockState.LightEmission);
                        }

                        byte reseedLevel = LightUtils.GetBlockLight(LightData[index]);
                        blockReseedQueue.Enqueue(index | reseedLevel << LightBfs.LevelShift);
                    }
                }
            }
        }

        private void RemoveLight(ref NativeQueue<int> removalQueue, ref NativeQueue<int> reseedQueue, bool isSun)
        {
            while (removalQueue.Count > 0)
            {
                int packed = removalQueue.Dequeue();
                int index = packed & LightBfs.IndexMask;
                byte oldLight = (byte)(packed >> LightBfs.LevelShift & LightBfs.LevelMask);
                int skipMask = packed >> LightBfs.SkipShift & 0x3F;

                LightBfs.IndexToXYZ(index, out int x, out int y, out int z);

                if ((skipMask & 1 << LightBfs.DirNegX) == 0)
                {
                    TryRemoveNeighbor(x - 1, y, z, oldLight, isSun, false,
                        1 << LightBfs.DirPosX, ref removalQueue, ref reseedQueue);
                }

                if ((skipMask & 1 << LightBfs.DirPosX) == 0)
                {
                    TryRemoveNeighbor(x + 1, y, z, oldLight, isSun, false,
                        1 << LightBfs.DirNegX, ref removalQueue, ref reseedQueue);
                }

                if ((skipMask & 1 << LightBfs.DirNegY) == 0)
                {
                    TryRemoveNeighbor(x, y - 1, z, oldLight, isSun, true,
                        1 << LightBfs.DirPosY, ref removalQueue, ref reseedQueue);
                }

                if ((skipMask & 1 << LightBfs.DirPosY) == 0)
                {
                    TryRemoveNeighbor(x, y + 1, z, oldLight, isSun, false,
                        1 << LightBfs.DirNegY, ref removalQueue, ref reseedQueue);
                }

                if ((skipMask & 1 << LightBfs.DirNegZ) == 0)
                {
                    TryRemoveNeighbor(x, y, z - 1, oldLight, isSun, false,
                        1 << LightBfs.DirPosZ, ref removalQueue, ref reseedQueue);
                }

                if ((skipMask & 1 << LightBfs.DirPosZ) == 0)
                {
                    TryRemoveNeighbor(x, y, z + 1, oldLight, isSun, false,
                        1 << LightBfs.DirNegZ, ref removalQueue, ref reseedQueue);
                }
            }
        }

        private void TryRemoveNeighbor(int nx, int ny, int nz, byte oldLight, bool isSun,
            bool isDownward, int neighborSkipMask,
            ref NativeQueue<int> removalQueue, ref NativeQueue<int> reseedQueue)
        {
            if (nx < 0 || nx >= ChunkConstants.Size ||
                ny < 0 || ny >= ChunkConstants.Size ||
                nz < 0 || nz >= ChunkConstants.Size)
            {
                return;
            }

            int neighborIndex = Voxel.Chunk.ChunkData.GetIndex(nx, ny, nz);
            byte neighborPacked = LightData[neighborIndex];
            byte neighborLight = isSun
                ? LightUtils.GetSunLight(neighborPacked)
                : LightUtils.GetBlockLight(neighborPacked);

            // Sunlight column special case: when removing sunlight at level 15 going
            // downward, force-remove the neighbor regardless of its level. Sky light
            // at 15 propagates downward without decrement, so the standard
            // "neighborLight < oldLight" check fails (both are 15). Without this,
            // the entire sunlight column below a placed block stays incorrectly lit.
            if (isSun && isDownward && oldLight == 15 && neighborLight > 0)
            {
                byte blockLight = LightUtils.GetBlockLight(neighborPacked);
                LightData[neighborIndex] = LightUtils.Pack(0, blockLight);
                removalQueue.Enqueue(neighborIndex | neighborLight << LightBfs.LevelShift
                                                   | neighborSkipMask << LightBfs.SkipShift);
            }
            else if (neighborLight > 0 && neighborLight < oldLight)
            {
                // This neighbor was lit by the removed source — remove it too
                if (isSun)
                {
                    byte blockLight = LightUtils.GetBlockLight(neighborPacked);
                    LightData[neighborIndex] = LightUtils.Pack(0, blockLight);
                }
                else
                {
                    byte sunLight = LightUtils.GetSunLight(neighborPacked);
                    LightData[neighborIndex] = LightUtils.Pack(sunLight, 0);
                }

                removalQueue.Enqueue(neighborIndex | neighborLight << LightBfs.LevelShift
                                                   | neighborSkipMask << LightBfs.SkipShift);
            }
            else if (neighborLight >= oldLight)
            {
                // This neighbor has light from another source — re-seed it
                byte reseedLevel = isSun
                    ? LightUtils.GetSunLight(LightData[neighborIndex])
                    : LightUtils.GetBlockLight(LightData[neighborIndex]);
                reseedQueue.Enqueue(neighborIndex | reseedLevel << LightBfs.LevelShift);
            }
        }

        /// <summary>
        ///     Checks a horizontal neighbor of a column-write-zeroed voxel. If the neighbor
        ///     has sunlight > 0 and is not opaque, enqueue it as a reseed (not removal).
        ///     This is the lightweight alternative to enqueueing the zeroed voxel into the
        ///     removal BFS queue — we directly find the boundary between zeroed and lit voxels.
        /// </summary>
        private void TryReseedHorizontalNeighbor(int nx, int ny, int nz,
            ref NativeQueue<int> sunReseedQueue)
        {
            if (nx < 0 || nx >= ChunkConstants.Size ||
                ny < 0 || ny >= ChunkConstants.Size ||
                nz < 0 || nz >= ChunkConstants.Size)
            {
                return;
            }

            int neighborIndex = Voxel.Chunk.ChunkData.GetIndex(nx, ny, nz);
            StateId neighborState = ChunkData[neighborIndex];

            if (StateTable[neighborState.Value].IsOpaque)
            {
                return;
            }

            byte neighborSun = LightUtils.GetSunLight(LightData[neighborIndex]);

            if (neighborSun > 0)
            {
                sunReseedQueue.Enqueue(neighborIndex | neighborSun << LightBfs.LevelShift);
            }
        }
    }
}
