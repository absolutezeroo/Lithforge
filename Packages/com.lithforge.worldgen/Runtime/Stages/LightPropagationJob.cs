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
    /// Blittable struct for border light entries produced by the propagation job.
    /// Stored in a NativeList and read back on the main thread.
    /// </summary>
    public struct NativeBorderLightEntry
    {
        public int3 LocalPosition;
        public byte PackedLight;
        public byte Face;
    }

    [BurstCompile]
    public struct LightPropagationJob : IJob
    {
        [ReadOnly] [NativeDisableContainerSafetyRestriction] public NativeArray<StateId> ChunkData;
        [ReadOnly] public NativeArray<BlockStateCompact> StateTable;

        public NativeArray<byte> LightData;

        /// <summary>
        /// Output: border light entries for cross-chunk propagation.
        /// Contains voxels at chunk faces with light > 1 that should propagate to neighbors.
        /// Owner: caller (GenerationScheduler). Dispose: caller after reading.
        /// </summary>
        public NativeList<NativeBorderLightEntry> BorderLightOutput;

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

            // Collect border light leaks for cross-chunk propagation
            CollectBorderLightLeaks();
        }

        /// <summary>
        /// After propagation completes, scan all 6 faces of the chunk for voxels
        /// with light > 1. These are "leaks" that need to propagate to neighbors.
        /// </summary>
        private void CollectBorderLightLeaks()
        {
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

        // ═══════════════════════════════════════════════════════════════════════
        // SHARED BFS LOGIC — duplicated in: LightPropagationJob, LightRemovalJob, LightUpdateJob
        // Any modification MUST be applied to all three files in the same commit.
        // ═══════════════════════════════════════════════════════════════════════

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
