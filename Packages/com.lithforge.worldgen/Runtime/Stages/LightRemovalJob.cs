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
    /// <summary>
    /// BFS light removal job for block-change-triggered and cross-chunk light recalculation.
    /// When a block is placed or removed, this job:
    /// 1. Seeds removal queues from border removal seeds (cross-chunk cascade) and
    ///    changed positions (block edits).
    /// 2. Removes stale light values via BFS, with a sunlight column special case
    ///    (downward at max light forces removal regardless of neighbor level).
    /// 3. Collects "re-seed" points (voxels where removed light meets still-valid light).
    /// 4. Re-propagates light from the re-seed points.
    /// 5. Scans chunk borders into BorderLightOutput for cross-chunk cascade.
    ///
    /// Owner of ChangedIndices: caller. Dispose: caller after Complete().
    /// Owner of BorderRemovalSeeds: caller. Dispose: caller after Complete().
    /// Owner of BorderLightOutput: caller. Dispose: caller after Complete().
    /// Owner of LightData: ManagedChunk (Persistent). Not disposed by this job.
    /// Owner of ChunkData: ManagedChunk (via ChunkPool, Persistent). Not disposed by this job.
    /// Owner of StateTable: NativeStateRegistry (Persistent). Not disposed by this job.
    /// </summary>
    [BurstCompile]
    public struct LightRemovalJob : IJob
    {
        public NativeArray<byte> LightData;

        [ReadOnly] public NativeArray<StateId> ChunkData;
        [ReadOnly] public NativeArray<BlockStateCompact> StateTable;

        /// <summary>
        /// Per-column surface heightmap from world generation. Used for sunlight column
        /// restoration at chunk-top (y=31) where checking the above voxel is impossible.
        /// Index: z * ChunkConstants.Size + x. Value: world-space Y of highest opaque block.
        /// Uses NativeDisableContainerSafetyRestriction because the job may write HeightMap
        /// when blocks are placed/removed (one job per chunk, gated by LightJobInFlight).
        /// Owner: ManagedChunk. Not disposed by this job.
        /// </summary>
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<int> HeightMap;

        /// <summary>
        /// World-space Y coordinate of the chunk's bottom (chunkCoord.y * ChunkConstants.Size).
        /// Used to convert local Y to world Y for HeightMap comparison.
        /// </summary>
        [ReadOnly] public int ChunkWorldY;

        /// <summary>
        /// Flat indices of changed voxels that triggered the relight.
        /// Uses NativeDisableContainerSafetyRestriction because a single persistent
        /// array may be shared across concurrent LightRemovalJob instances (each
        /// reading the same full-chunk index list). This is safe because the field
        /// is read-only and all jobs read identical data.
        /// Owner: caller. Dispose: caller after Complete().
        /// </summary>
        [ReadOnly]
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<int> ChangedIndices;

        /// <summary>
        /// Border removal seeds from neighboring chunk edits. Each entry specifies a
        /// border voxel whose incoming cross-chunk light has decreased and needs removal.
        /// The LocalPosition is in THIS chunk's coordinate space.
        /// Owner: caller. Dispose: caller after Complete().
        /// </summary>
        [ReadOnly]
        public NativeArray<NativeBorderLightEntry> BorderRemovalSeeds;

        /// <summary>
        /// Output: border light entries after removal+reseed completes.
        /// Contains voxels at chunk faces with light > 1 for cross-chunk cascade.
        /// The caller compares these with the chunk's old border entries to detect
        /// changes that need to propagate to further neighbors.
        /// Owner: caller. Dispose: caller after reading.
        /// </summary>
        public NativeList<NativeBorderLightEntry> BorderLightOutput;

        public void Execute()
        {
            // Queue entries: packed as (index << 8) | oldLightValue
            // This lets us track what light level was removed at each position
            NativeQueue<int> sunRemovalQueue = new NativeQueue<int>(Allocator.TempJob);
            NativeQueue<int> blockRemovalQueue = new NativeQueue<int>(Allocator.TempJob);
            NativeQueue<int> sunReseedQueue = new NativeQueue<int>(Allocator.TempJob);
            NativeQueue<int> blockReseedQueue = new NativeQueue<int>(Allocator.TempJob);

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
            RepropagateSun(ref sunReseedQueue);
            RepropagateBlock(ref blockReseedQueue);

            // Step 5: Scan borders for cross-chunk cascade
            CollectBorderLightLeaks();

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
                int3 pos = seed.LocalPosition;

                if (pos.x < 0 || pos.x >= ChunkConstants.Size ||
                    pos.y < 0 || pos.y >= ChunkConstants.Size ||
                    pos.z < 0 || pos.z >= ChunkConstants.Size)
                {
                    continue;
                }

                int index = Lithforge.Voxel.Chunk.ChunkData.GetIndex(pos.x, pos.y, pos.z);
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
                    sunRemovalQueue.Enqueue((index << 8) | currentSun);
                }

                if (currentBlock > 0 && blockState.LightEmission == 0)
                {
                    LightData[index] = LightUtils.Pack(LightUtils.GetSunLight(LightData[index]), 0);
                    blockRemovalQueue.Enqueue((index << 8) | currentBlock);
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

                    blockReseedQueue.Enqueue(index);
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

                    if (sun > 0)
                    {
                        sunRemovalQueue.Enqueue((index << 8) | sun);
                    }

                    if (block > 0)
                    {
                        blockRemovalQueue.Enqueue((index << 8) | block);
                    }

                    // Raise heightmap if placing a block above the current surface
                    if (HeightMap.IsCreated)
                    {
                        IndexToXYZ(index, out int hx, out int hy, out int hz);
                        int colIdx = hz * ChunkConstants.Size + hx;
                        int blockWorldY = ChunkWorldY + hy;

                        if (blockWorldY > HeightMap[colIdx])
                        {
                            HeightMap[colIdx] = blockWorldY;
                        }
                    }
                }
                else
                {
                    // Sunlight column restoration check: if the voxel is at or above the
                    // heightmap surface, it's in direct sunlight. Falls back to checking
                    // the above voxel if no heightmap is available.
                    IndexToXYZ(index, out int ax, out int ay, out int az);
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
                        // Fallback: check voxel above (old behavior, no heightmap)
                        int aboveIndex = Lithforge.Voxel.Chunk.ChunkData.GetIndex(ax, ay + 1, az);
                        byte aboveSun = LightUtils.GetSunLight(LightData[aboveIndex]);

                        if (aboveSun == 15)
                        {
                            sunColumnRestore = true;
                        }
                    }

                    if (sunColumnRestore)
                    {
                        // Column restoration: set sun=15 directly and reseed. Skip removal
                        // to avoid a stale removal entry cascading downward at max light.
                        byte currentBlockLight = LightUtils.GetBlockLight(LightData[index]);
                        LightData[index] = LightUtils.Pack(15, currentBlockLight);
                        sunReseedQueue.Enqueue(index);
                    }
                    else if (sun > 0)
                    {
                        // No column above — remove old sun and let it re-seed from neighbors
                        LightData[index] = LightUtils.Pack(0, LightUtils.GetBlockLight(LightData[index]));
                        sunRemovalQueue.Enqueue((index << 8) | sun);
                    }

                    if (block > 0 && blockState.LightEmission == 0)
                    {
                        LightData[index] = LightUtils.Pack(LightUtils.GetSunLight(LightData[index]), 0);
                        blockRemovalQueue.Enqueue((index << 8) | block);
                    }

                    // If this block emits light, seed it for re-propagation
                    if (blockState.LightEmission > 0)
                    {
                        byte currentBlock = LightUtils.GetBlockLight(LightData[index]);

                        if (blockState.LightEmission > currentBlock)
                        {
                            byte currentSun = LightUtils.GetSunLight(LightData[index]);
                            LightData[index] = LightUtils.Pack(currentSun, blockState.LightEmission);
                        }

                        blockReseedQueue.Enqueue(index);
                    }

                    // Lower heightmap if removing a block at or above the surface.
                    // Note: scan is chunk-local only. If no opaque block is found in this
                    // chunk, newSurface = ChunkWorldY - 1. The actual surface may be in a
                    // lower chunk, but the heightmap is per-chunk so this is the best we can do.
                    if (HeightMap.IsCreated)
                    {
                        int colIdx = az * ChunkConstants.Size + ax;
                        int blockWorldY = ChunkWorldY + ay;

                        if (blockWorldY >= HeightMap[colIdx])
                        {
                            // Scan downward to find the new highest opaque block
                            int newSurface = ChunkWorldY - 1;

                            for (int scanY = ay - 1; scanY >= 0; scanY--)
                            {
                                int scanIdx = Lithforge.Voxel.Chunk.ChunkData.GetIndex(ax, scanY, az);

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
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // SHARED BORDER COLLECTION LOGIC — duplicated in: LightPropagationJob, LightRemovalJob
        // Any modification MUST be applied to both files in the same commit.
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// After removal+reseed completes, scan all 6 faces for voxels with light > 1.
        /// These represent the chunk's new border light state for cross-chunk propagation.
        /// </summary>
        private void CollectBorderLightLeaks()
        {
            if (!BorderLightOutput.IsCreated)
            {
                return;
            }

            int lastIdx = ChunkConstants.Size - 1;

            for (int a = 0; a < ChunkConstants.Size; a++)
            {
                for (int b = 0; b < ChunkConstants.Size; b++)
                {
                    // +X face (x=31)
                    CollectBorderVoxel(lastIdx, a, b, new int3(lastIdx, a, b), 0);
                    // -X face (x=0)
                    CollectBorderVoxel(0, a, b, new int3(0, a, b), 1);
                    // +Y face (y=31)
                    CollectBorderVoxel(a, lastIdx, b, new int3(a, lastIdx, b), 2);
                    // -Y face (y=0)
                    CollectBorderVoxel(a, 0, b, new int3(a, 0, b), 3);
                    // +Z face (z=31)
                    CollectBorderVoxel(a, b, lastIdx, new int3(a, b, lastIdx), 4);
                    // -Z face (z=0)
                    CollectBorderVoxel(a, b, 0, new int3(a, b, 0), 5);
                }
            }
        }

        private void CollectBorderVoxel(int x, int y, int z, int3 localPos, byte face)
        {
            int index = Lithforge.Voxel.Chunk.ChunkData.GetIndex(x, y, z);
            byte packed = LightData[index];
            byte sun = LightUtils.GetSunLight(packed);
            byte block = LightUtils.GetBlockLight(packed);

            if (sun > 1 || block > 1)
            {
                BorderLightOutput.Add(new NativeBorderLightEntry
                {
                    LocalPosition = localPos,
                    PackedLight = packed,
                    Face = face,
                });
            }
        }

        private void RemoveLight(ref NativeQueue<int> removalQueue, ref NativeQueue<int> reseedQueue, bool isSun)
        {
            while (removalQueue.Count > 0)
            {
                int packed = removalQueue.Dequeue();
                int index = packed >> 8;
                byte oldLight = (byte)(packed & 0xFF);

                IndexToXYZ(index, out int x, out int y, out int z);

                TryRemoveNeighbor(x - 1, y, z, oldLight, isSun, false, ref removalQueue, ref reseedQueue);
                TryRemoveNeighbor(x + 1, y, z, oldLight, isSun, false, ref removalQueue, ref reseedQueue);
                TryRemoveNeighbor(x, y - 1, z, oldLight, isSun, true, ref removalQueue, ref reseedQueue);
                TryRemoveNeighbor(x, y + 1, z, oldLight, isSun, false, ref removalQueue, ref reseedQueue);
                TryRemoveNeighbor(x, y, z - 1, oldLight, isSun, false, ref removalQueue, ref reseedQueue);
                TryRemoveNeighbor(x, y, z + 1, oldLight, isSun, false, ref removalQueue, ref reseedQueue);
            }
        }

        private void TryRemoveNeighbor(int nx, int ny, int nz, byte oldLight, bool isSun,
            bool isDownward, ref NativeQueue<int> removalQueue, ref NativeQueue<int> reseedQueue)
        {
            if (nx < 0 || nx >= ChunkConstants.Size ||
                ny < 0 || ny >= ChunkConstants.Size ||
                nz < 0 || nz >= ChunkConstants.Size)
            {
                return;
            }

            int neighborIndex = Lithforge.Voxel.Chunk.ChunkData.GetIndex(nx, ny, nz);
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
                removalQueue.Enqueue((neighborIndex << 8) | neighborLight);
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

                removalQueue.Enqueue((neighborIndex << 8) | neighborLight);
            }
            else if (neighborLight >= oldLight)
            {
                // This neighbor has light from another source — re-seed it
                reseedQueue.Enqueue(neighborIndex);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // SHARED BFS LOGIC — duplicated in: LightPropagationJob, LightRemovalJob, LightUpdateJob
        // Any modification MUST be applied to all three files in the same commit.
        // ═══════════════════════════════════════════════════════════════════════

        // Direction flag constants for skip-back-propagation optimization.
        // Queue entry packing: int packed = index | (skipMask << _skipShift)
        // index: 15 bits (0..32767), skipMask: 6 bits (one per direction).
        // When bit N is set, direction N is skipped (it was the source direction).
        // SHARED CONSTANTS — duplicated in: LightPropagationJob, LightRemovalJob, LightUpdateJob
        private const int _dirNegX = 0;
        private const int _dirPosX = 1;
        private const int _dirNegY = 2;
        private const int _dirPosY = 3;
        private const int _dirNegZ = 4;
        private const int _dirPosZ = 5;
        private const int _indexBits = 15;
        private const int _indexMask = (1 << _indexBits) - 1;
        private const int _skipShift = _indexBits;

        private void RepropagateSun(ref NativeQueue<int> queue)
        {
            while (queue.Count > 0)
            {
                int packed = queue.Dequeue();
                int index = packed & _indexMask;
                int skipMask = (packed >> _skipShift) & 0x3F;
                byte packedLight = LightData[index];
                byte currentSun = LightUtils.GetSunLight(packedLight);

                if (currentSun <= 1)
                {
                    continue;
                }

                IndexToXYZ(index, out int x, out int y, out int z);

                if ((skipMask & (1 << _dirNegX)) == 0)
                {
                    TryPropagateSun(x - 1, y, z, currentSun, false, 1 << _dirPosX, ref queue);
                }

                if ((skipMask & (1 << _dirPosX)) == 0)
                {
                    TryPropagateSun(x + 1, y, z, currentSun, false, 1 << _dirNegX, ref queue);
                }

                if ((skipMask & (1 << _dirNegY)) == 0)
                {
                    TryPropagateSun(x, y - 1, z, currentSun, true, 1 << _dirPosY, ref queue);
                }

                if ((skipMask & (1 << _dirPosY)) == 0)
                {
                    TryPropagateSun(x, y + 1, z, currentSun, false, 1 << _dirNegY, ref queue);
                }

                if ((skipMask & (1 << _dirNegZ)) == 0)
                {
                    TryPropagateSun(x, y, z - 1, currentSun, false, 1 << _dirPosZ, ref queue);
                }

                if ((skipMask & (1 << _dirPosZ)) == 0)
                {
                    TryPropagateSun(x, y, z + 1, currentSun, false, 1 << _dirNegZ, ref queue);
                }
            }
        }

        private void TryPropagateSun(int nx, int ny, int nz, byte sourceSun, bool isDownward,
            int neighborSkipMask, ref NativeQueue<int> queue)
        {
            if (nx < 0 || nx >= ChunkConstants.Size ||
                ny < 0 || ny >= ChunkConstants.Size ||
                nz < 0 || nz >= ChunkConstants.Size)
            {
                return;
            }

            int neighborIndex = Lithforge.Voxel.Chunk.ChunkData.GetIndex(nx, ny, nz);
            StateId neighborState = ChunkData[neighborIndex];
            BlockStateCompact neighborBlockState = StateTable[neighborState.Value];

            if (neighborBlockState.IsOpaque)
            {
                return;
            }

            byte filter = neighborBlockState.LightFilter;
            byte newSun;

            if (isDownward && sourceSun == 15 && !neighborBlockState.IsOpaque)
            {
                newSun = 15;
            }
            else
            {
                int attenuation = filter > 0 ? filter : 1;
                int reduced = sourceSun - attenuation;
                newSun = (byte)(reduced > 0 ? reduced : 0);
            }

            if (newSun == 0)
            {
                return;
            }

            byte neighborPacked = LightData[neighborIndex];
            byte neighborSun = LightUtils.GetSunLight(neighborPacked);

            if (newSun > neighborSun)
            {
                byte neighborBlock = LightUtils.GetBlockLight(neighborPacked);
                LightData[neighborIndex] = LightUtils.Pack(newSun, neighborBlock);
                queue.Enqueue(neighborIndex | (neighborSkipMask << _skipShift));
            }
        }

        private void RepropagateBlock(ref NativeQueue<int> queue)
        {
            while (queue.Count > 0)
            {
                int packed = queue.Dequeue();
                int index = packed & _indexMask;
                int skipMask = (packed >> _skipShift) & 0x3F;
                byte packedLight = LightData[index];
                byte currentBlock = LightUtils.GetBlockLight(packedLight);

                if (currentBlock <= 1)
                {
                    continue;
                }

                IndexToXYZ(index, out int x, out int y, out int z);

                if ((skipMask & (1 << _dirNegX)) == 0)
                {
                    TryPropagateBlock(x - 1, y, z, currentBlock, 1 << _dirPosX, ref queue);
                }

                if ((skipMask & (1 << _dirPosX)) == 0)
                {
                    TryPropagateBlock(x + 1, y, z, currentBlock, 1 << _dirNegX, ref queue);
                }

                if ((skipMask & (1 << _dirNegY)) == 0)
                {
                    TryPropagateBlock(x, y - 1, z, currentBlock, 1 << _dirPosY, ref queue);
                }

                if ((skipMask & (1 << _dirPosY)) == 0)
                {
                    TryPropagateBlock(x, y + 1, z, currentBlock, 1 << _dirNegY, ref queue);
                }

                if ((skipMask & (1 << _dirNegZ)) == 0)
                {
                    TryPropagateBlock(x, y, z - 1, currentBlock, 1 << _dirPosZ, ref queue);
                }

                if ((skipMask & (1 << _dirPosZ)) == 0)
                {
                    TryPropagateBlock(x, y, z + 1, currentBlock, 1 << _dirNegZ, ref queue);
                }
            }
        }

        private void TryPropagateBlock(int nx, int ny, int nz, byte sourceBlock,
            int neighborSkipMask, ref NativeQueue<int> queue)
        {
            if (nx < 0 || nx >= ChunkConstants.Size ||
                ny < 0 || ny >= ChunkConstants.Size ||
                nz < 0 || nz >= ChunkConstants.Size)
            {
                return;
            }

            int neighborIndex = Lithforge.Voxel.Chunk.ChunkData.GetIndex(nx, ny, nz);
            StateId neighborState = ChunkData[neighborIndex];
            BlockStateCompact neighborBlockState = StateTable[neighborState.Value];

            if (neighborBlockState.IsOpaque)
            {
                return;
            }

            byte filter = neighborBlockState.LightFilter;
            int attenuation = filter > 0 ? filter : 1;
            int newBlock = sourceBlock - attenuation;

            if (newBlock <= 0)
            {
                return;
            }

            byte neighborPacked = LightData[neighborIndex];
            byte neighborBlockLight = LightUtils.GetBlockLight(neighborPacked);

            if ((byte)newBlock > neighborBlockLight)
            {
                byte neighborSun = LightUtils.GetSunLight(neighborPacked);
                LightData[neighborIndex] = LightUtils.Pack(neighborSun, (byte)newBlock);
                queue.Enqueue(neighborIndex | (neighborSkipMask << _skipShift));
            }
        }

        private static void IndexToXYZ(int index, out int x, out int y, out int z)
        {
            x = index & ChunkConstants.SizeMask;
            z = (index >> ChunkConstants.SizeBits) & ChunkConstants.SizeMask;
            y = (index >> (ChunkConstants.SizeBits * 2)) & ChunkConstants.SizeMask;
        }
    }
}
