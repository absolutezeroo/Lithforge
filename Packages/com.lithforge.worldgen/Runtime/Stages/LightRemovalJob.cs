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
    /// BFS light removal job for block-change-triggered light recalculation.
    /// When a block is placed or removed, this job:
    /// 1. Removes stale light values via BFS from changed positions.
    /// 2. Collects "re-seed" points (border voxels where removed light meets
    ///    still-valid light from adjacent sources).
    /// 3. Re-propagates light from the re-seed points.
    ///
    /// Owner of ChangedIndices: caller. Dispose: caller after Complete().
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

        public void Execute()
        {
            // Queue entries: packed as (index << 8) | oldLightValue
            // This lets us track what light level was removed at each position
            NativeQueue<int> sunRemovalQueue = new NativeQueue<int>(Allocator.TempJob);
            NativeQueue<int> blockRemovalQueue = new NativeQueue<int>(Allocator.TempJob);
            NativeQueue<int> sunReseedQueue = new NativeQueue<int>(Allocator.TempJob);
            NativeQueue<int> blockReseedQueue = new NativeQueue<int>(Allocator.TempJob);

            // Step 1: Seed removal queues from changed positions
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
                }
                else
                {
                    // Block was removed (now air/transparent) — remove old light
                    // and let it be re-seeded from neighbors
                    if (sun > 0)
                    {
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
                }
            }

            // Step 2: BFS removal of sunlight
            RemoveLight(ref sunRemovalQueue, ref sunReseedQueue, true);

            // Step 3: BFS removal of block light
            RemoveLight(ref blockRemovalQueue, ref blockReseedQueue, false);

            // Step 4: Re-propagate from re-seed points
            RepropagateSun(ref sunReseedQueue);
            RepropagateBlock(ref blockReseedQueue);

            sunRemovalQueue.Dispose();
            blockRemovalQueue.Dispose();
            sunReseedQueue.Dispose();
            blockReseedQueue.Dispose();
        }

        private void RemoveLight(ref NativeQueue<int> removalQueue, ref NativeQueue<int> reseedQueue, bool isSun)
        {
            while (removalQueue.Count > 0)
            {
                int packed = removalQueue.Dequeue();
                int index = packed >> 8;
                byte oldLight = (byte)(packed & 0xFF);

                IndexToXYZ(index, out int x, out int y, out int z);

                TryRemoveNeighbor(x - 1, y, z, oldLight, isSun, ref removalQueue, ref reseedQueue);
                TryRemoveNeighbor(x + 1, y, z, oldLight, isSun, ref removalQueue, ref reseedQueue);
                TryRemoveNeighbor(x, y - 1, z, oldLight, isSun, ref removalQueue, ref reseedQueue);
                TryRemoveNeighbor(x, y + 1, z, oldLight, isSun, ref removalQueue, ref reseedQueue);
                TryRemoveNeighbor(x, y, z - 1, oldLight, isSun, ref removalQueue, ref reseedQueue);
                TryRemoveNeighbor(x, y, z + 1, oldLight, isSun, ref removalQueue, ref reseedQueue);
            }
        }

        private void TryRemoveNeighbor(int nx, int ny, int nz, byte oldLight, bool isSun,
            ref NativeQueue<int> removalQueue, ref NativeQueue<int> reseedQueue)
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

            if (neighborLight > 0 && neighborLight < oldLight)
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

        private void RepropagateSun(ref NativeQueue<int> queue)
        {
            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                byte packed = LightData[index];
                byte currentSun = LightUtils.GetSunLight(packed);

                if (currentSun <= 1)
                {
                    continue;
                }

                IndexToXYZ(index, out int x, out int y, out int z);

                TryPropagateSun(x - 1, y, z, currentSun, false, ref queue);
                TryPropagateSun(x + 1, y, z, currentSun, false, ref queue);
                TryPropagateSun(x, y - 1, z, currentSun, true, ref queue);
                TryPropagateSun(x, y + 1, z, currentSun, false, ref queue);
                TryPropagateSun(x, y, z - 1, currentSun, false, ref queue);
                TryPropagateSun(x, y, z + 1, currentSun, false, ref queue);
            }
        }

        private void TryPropagateSun(int nx, int ny, int nz, byte sourceSun, bool isDownward, ref NativeQueue<int> queue)
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
                queue.Enqueue(neighborIndex);
            }
        }

        private void RepropagateBlock(ref NativeQueue<int> queue)
        {
            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                byte packed = LightData[index];
                byte currentBlock = LightUtils.GetBlockLight(packed);

                if (currentBlock <= 1)
                {
                    continue;
                }

                IndexToXYZ(index, out int x, out int y, out int z);

                TryPropagateBlock(x - 1, y, z, currentBlock, ref queue);
                TryPropagateBlock(x + 1, y, z, currentBlock, ref queue);
                TryPropagateBlock(x, y - 1, z, currentBlock, ref queue);
                TryPropagateBlock(x, y + 1, z, currentBlock, ref queue);
                TryPropagateBlock(x, y, z - 1, currentBlock, ref queue);
                TryPropagateBlock(x, y, z + 1, currentBlock, ref queue);
            }
        }

        private void TryPropagateBlock(int nx, int ny, int nz, byte sourceBlock, ref NativeQueue<int> queue)
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
                queue.Enqueue(neighborIndex);
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
