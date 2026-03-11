using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.WorldGen.Lighting;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lithforge.WorldGen.Stages
{
    /// <summary>
    /// Lightweight cross-chunk light update job. Seeds border voxels from neighbor
    /// border light values and propagates only the delta (voxels where incoming light
    /// exceeds existing light). Does NOT do a full re-propagation.
    ///
    /// Owner of SeedEntries: caller (GenerationScheduler). Dispose: caller after Complete().
    /// Owner of LightData: ManagedChunk (Persistent). Not disposed by this job.
    /// Owner of ChunkData: ManagedChunk (via ChunkPool, Persistent). Not disposed by this job.
    /// Owner of StateTable: NativeStateRegistry (Persistent). Not disposed by this job.
    /// </summary>
    [BurstCompile]
    public struct LightUpdateJob : IJob
    {
        public NativeArray<byte> LightData;

        [ReadOnly] public NativeArray<StateId> ChunkData;
        [ReadOnly] public NativeArray<BlockStateCompact> StateTable;

        /// <summary>
        /// Seed entries: border light values from neighboring chunks, mapped to local
        /// coordinates in this chunk. Each entry's LocalPosition is the position in THIS
        /// chunk where the neighbor's light should seed.
        /// Owner: caller. Dispose: caller after Complete().
        /// </summary>
        [ReadOnly] public NativeArray<NativeBorderLightEntry> SeedEntries;

        public void Execute()
        {
            NativeQueue<int> sunQueue = new NativeQueue<int>(Allocator.TempJob);
            NativeQueue<int> blockQueue = new NativeQueue<int>(Allocator.TempJob);

            // Seed from neighbor border values
            for (int i = 0; i < SeedEntries.Length; i++)
            {
                NativeBorderLightEntry seed = SeedEntries[i];
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

                byte incomingSun = LightUtils.GetSunLight(seed.PackedLight);
                byte incomingBlock = LightUtils.GetBlockLight(seed.PackedLight);

                // Attenuate by 1 (crossing chunk boundary counts as one step)
                byte filter = blockState.LightFilter;
                int sunAttenuation = filter > 0 ? filter : 1;
                int blockAttenuation = filter > 0 ? filter : 1;

                // Special case: sunlight going straight down (face 2 = +Y of neighbor = -Y into this chunk)
                bool isSunDown = seed.Face == 2 && incomingSun == 15;
                int newSun = isSunDown ? 15 : incomingSun - sunAttenuation;
                int newBlock = incomingBlock - blockAttenuation;

                if (newSun < 0) { newSun = 0; }
                if (newBlock < 0) { newBlock = 0; }

                byte currentPacked = LightData[index];
                byte currentSun = LightUtils.GetSunLight(currentPacked);
                byte currentBlock = LightUtils.GetBlockLight(currentPacked);

                bool changed = false;

                if ((byte)newSun > currentSun)
                {
                    currentSun = (byte)newSun;
                    changed = true;
                }

                if ((byte)newBlock > currentBlock)
                {
                    currentBlock = (byte)newBlock;
                    changed = true;
                }

                if (changed)
                {
                    LightData[index] = LightUtils.Pack(currentSun, currentBlock);

                    if (currentSun > 1)
                    {
                        sunQueue.Enqueue(index);
                    }

                    if (currentBlock > 1)
                    {
                        blockQueue.Enqueue(index);
                    }
                }
            }

            // Propagate sunlight delta
            PropagateSun(ref sunQueue);

            // Propagate block light delta
            PropagateBlock(ref blockQueue);

            sunQueue.Dispose();
            blockQueue.Dispose();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // SHARED BFS LOGIC — duplicated in: LightPropagationJob, LightRemovalJob, LightUpdateJob
        // Any modification MUST be applied to all three files in the same commit.
        // ═══════════════════════════════════════════════════════════════════════

        private void PropagateSun(ref NativeQueue<int> queue)
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

        private void PropagateBlock(ref NativeQueue<int> queue)
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
