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

            // Seed queues with all voxels that have light > 0.
            // Seeds use bare index (no skip mask): i < Volume = 1 << _indexBits,
            // so upper bits are 0 and skipMask decodes as 0 (all directions propagated).
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

        private void PropagateSunlight(ref NativeQueue<int> queue)
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
                queue.Enqueue(neighborIndex | (neighborSkipMask << _skipShift));
            }
        }

        private void PropagateBlockLight(ref NativeQueue<int> queue)
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
            // index = y * SizeSquared + z * Size + x
            x = index & ChunkConstants.SizeMask;
            z = (index >> ChunkConstants.SizeBits) & ChunkConstants.SizeMask;
            y = (index >> (ChunkConstants.SizeBits * 2)) & ChunkConstants.SizeMask;
        }
    }
}
