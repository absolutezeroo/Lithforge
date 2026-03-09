using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.WorldGen.Lighting;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Lithforge.WorldGen.Stages
{
    [BurstCompile]
    public struct LightPropagationJob : IJob
    {
        [ReadOnly] public NativeArray<StateId> ChunkData;
        [ReadOnly] public NativeArray<BlockStateCompact> StateTable;

        public NativeArray<byte> LightData;

        public void Execute()
        {
            NativeQueue<int> sunQueue = new NativeQueue<int>(Allocator.TempJob);
            NativeQueue<int> blockQueue = new NativeQueue<int>(Allocator.TempJob);

            // Seed queues with all voxels that have light > 0
            for (int i = 0; i < ChunkConstants.Volume; i++)
            {
                byte packed = LightData[i];
                byte sun = LightUtils.GetSunLight(packed);
                byte block = LightUtils.GetBlockLight(packed);

                if (sun > 0)
                {
                    sunQueue.Enqueue(i);
                }

                if (block > 0)
                {
                    blockQueue.Enqueue(i);
                }
            }

            // Propagate sunlight
            PropagateSunlight(ref sunQueue);

            // Propagate block light
            PropagateBlockLight(ref blockQueue);

            sunQueue.Dispose();
            blockQueue.Dispose();
        }

        private void PropagateSunlight(ref NativeQueue<int> queue)
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

                // 6 neighbors
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
            BlockStateCompact neighborBlock = StateTable[neighborState.Value];

            if (neighborBlock.IsOpaque)
            {
                return;
            }

            byte filter = neighborBlock.LightFilter;

            // Sunlight going straight down through air/transparent maintains level 15
            byte newSun;

            if (isDownward && sourceSun == 15 && !neighborBlock.IsOpaque)
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
                byte neighborBlock2 = LightUtils.GetBlockLight(neighborPacked);
                LightData[neighborIndex] = LightUtils.Pack(newSun, neighborBlock2);
                queue.Enqueue(neighborIndex);
            }
        }

        private void PropagateBlockLight(ref NativeQueue<int> queue)
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
            // index = y * SizeSquared + z * Size + x
            x = index & ChunkConstants.SizeMask;
            z = (index >> ChunkConstants.SizeBits) & ChunkConstants.SizeMask;
            y = (index >> (ChunkConstants.SizeBits * 2)) & ChunkConstants.SizeMask;
        }
    }
}
