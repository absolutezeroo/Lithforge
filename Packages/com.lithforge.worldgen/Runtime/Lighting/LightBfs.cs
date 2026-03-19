using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Lithforge.WorldGen.Lighting
{
    /// <summary>
    /// Shared BFS constants and propagation/collection methods used by all 3 light jobs.
    /// Single source of truth — any BFS logic change is applied here and affects all jobs.
    /// All methods are static and Burst-compatible (no managed types, no virtual calls).
    /// </summary>
    [BurstCompile]
    public readonly struct LightBfs
    {
        // Queue entry packing (25-bit unified encoding):
        //   [24..19: skipMask (6)] [18..15: level (4)] [14..0: index (15)]

        /// <summary>Direction index for -X neighbor.</summary>
        public const int DirNegX = 0;

        /// <summary>Direction index for +X neighbor.</summary>
        public const int DirPosX = 1;

        /// <summary>Direction index for -Y neighbor.</summary>
        public const int DirNegY = 2;

        /// <summary>Direction index for +Y neighbor.</summary>
        public const int DirPosY = 3;

        /// <summary>Direction index for -Z neighbor.</summary>
        public const int DirNegZ = 4;

        /// <summary>Direction index for +Z neighbor.</summary>
        public const int DirPosZ = 5;

        /// <summary>Number of bits used for the flat voxel index in queue entries.</summary>
        public const int IndexBits = 15;

        /// <summary>Bitmask to extract the flat voxel index from a packed queue entry.</summary>
        public const int IndexMask = (1 << IndexBits) - 1;

        /// <summary>Bit shift for the carried light level in queue entries.</summary>
        public const int LevelShift = 15;

        /// <summary>Bitmask to extract the 4-bit light level after shifting.</summary>
        public const int LevelMask = 0xF;

        /// <summary>Bit shift for the 6-bit direction skip mask in queue entries.</summary>
        public const int SkipShift = 19;

        /// <summary>Decomposes a flat voxel index into chunk-local XYZ coordinates.</summary>
        public static void IndexToXYZ(int index, out int x, out int y, out int z)
        {
            x = index & ChunkConstants.SizeMask;
            z = (index >> ChunkConstants.SizeBits) & ChunkConstants.SizeMask;
            y = (index >> (ChunkConstants.SizeBits * 2)) & ChunkConstants.SizeMask;
        }

        /// <summary>BFS propagation of sunlight through transparent blocks with downward-at-15 special case.</summary>
        public static void PropagateSun(
            ref NativeQueue<int> queue,
            ref NativeArray<byte> lightData,
            ref NativeArray<StateId> chunkData,
            ref NativeArray<BlockStateCompact> stateTable)
        {
            while (queue.Count > 0)
            {
                int packed = queue.Dequeue();
                int index = packed & IndexMask;
                int carriedLevel = (packed >> LevelShift) & LevelMask;
                int skipMask = (packed >> SkipShift) & 0x3F;
                byte packedLight = lightData[index];
                byte currentSun = LightUtils.GetSunLight(packedLight);

                if (currentSun <= 1 || carriedLevel != currentSun)
                {
                    continue;
                }

                IndexToXYZ(index, out int x, out int y, out int z);

                if ((skipMask & (1 << DirNegX)) == 0)
                {
                    TryPropagateSun(x - 1, y, z, currentSun, false, 1 << DirPosX, ref queue, ref lightData, ref chunkData, ref stateTable);
                }

                if ((skipMask & (1 << DirPosX)) == 0)
                {
                    TryPropagateSun(x + 1, y, z, currentSun, false, 1 << DirNegX, ref queue, ref lightData, ref chunkData, ref stateTable);
                }

                if ((skipMask & (1 << DirNegY)) == 0)
                {
                    TryPropagateSun(x, y - 1, z, currentSun, true, 1 << DirPosY, ref queue, ref lightData, ref chunkData, ref stateTable);
                }

                if ((skipMask & (1 << DirPosY)) == 0)
                {
                    TryPropagateSun(x, y + 1, z, currentSun, false, 1 << DirNegY, ref queue, ref lightData, ref chunkData, ref stateTable);
                }

                if ((skipMask & (1 << DirNegZ)) == 0)
                {
                    TryPropagateSun(x, y, z - 1, currentSun, false, 1 << DirPosZ, ref queue, ref lightData, ref chunkData, ref stateTable);
                }

                if ((skipMask & (1 << DirPosZ)) == 0)
                {
                    TryPropagateSun(x, y, z + 1, currentSun, false, 1 << DirNegZ, ref queue, ref lightData, ref chunkData, ref stateTable);
                }
            }
        }

        /// <summary>Attempts to propagate sunlight into a single neighbor voxel, maintaining level 15 downward through air.</summary>
        public static void TryPropagateSun(int nx, int ny, int nz, byte sourceSun, bool isDownward,
            int neighborSkipMask, ref NativeQueue<int> queue,
            ref NativeArray<byte> lightData, ref NativeArray<StateId> chunkData,
            ref NativeArray<BlockStateCompact> stateTable)
        {
            if (nx < 0 || nx >= ChunkConstants.Size ||
                ny < 0 || ny >= ChunkConstants.Size ||
                nz < 0 || nz >= ChunkConstants.Size)
            {
                return;
            }

            int neighborIndex = ChunkData.GetIndex(nx, ny, nz);
            StateId neighborState = chunkData[neighborIndex];
            BlockStateCompact neighborBlock = stateTable[neighborState.Value];

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

            byte neighborPacked = lightData[neighborIndex];
            byte neighborSun = LightUtils.GetSunLight(neighborPacked);

            if (newSun > neighborSun)
            {
                byte neighborBlockLight = LightUtils.GetBlockLight(neighborPacked);
                lightData[neighborIndex] = LightUtils.Pack(newSun, neighborBlockLight);
                queue.Enqueue(neighborIndex | ((int)newSun << LevelShift) | (neighborSkipMask << SkipShift));
            }
        }

        /// <summary>BFS propagation of block light through transparent blocks with filter attenuation.</summary>
        public static void PropagateBlock(
            ref NativeQueue<int> queue,
            ref NativeArray<byte> lightData,
            ref NativeArray<StateId> chunkData,
            ref NativeArray<BlockStateCompact> stateTable)
        {
            while (queue.Count > 0)
            {
                int packed = queue.Dequeue();
                int index = packed & IndexMask;
                int carriedLevel = (packed >> LevelShift) & LevelMask;
                int skipMask = (packed >> SkipShift) & 0x3F;
                byte packedLight = lightData[index];
                byte currentBlock = LightUtils.GetBlockLight(packedLight);

                if (currentBlock <= 1 || carriedLevel != currentBlock)
                {
                    continue;
                }

                IndexToXYZ(index, out int x, out int y, out int z);

                if ((skipMask & (1 << DirNegX)) == 0)
                {
                    TryPropagateBlock(x - 1, y, z, currentBlock, 1 << DirPosX, ref queue, ref lightData, ref chunkData, ref stateTable);
                }

                if ((skipMask & (1 << DirPosX)) == 0)
                {
                    TryPropagateBlock(x + 1, y, z, currentBlock, 1 << DirNegX, ref queue, ref lightData, ref chunkData, ref stateTable);
                }

                if ((skipMask & (1 << DirNegY)) == 0)
                {
                    TryPropagateBlock(x, y - 1, z, currentBlock, 1 << DirPosY, ref queue, ref lightData, ref chunkData, ref stateTable);
                }

                if ((skipMask & (1 << DirPosY)) == 0)
                {
                    TryPropagateBlock(x, y + 1, z, currentBlock, 1 << DirNegY, ref queue, ref lightData, ref chunkData, ref stateTable);
                }

                if ((skipMask & (1 << DirNegZ)) == 0)
                {
                    TryPropagateBlock(x, y, z - 1, currentBlock, 1 << DirPosZ, ref queue, ref lightData, ref chunkData, ref stateTable);
                }

                if ((skipMask & (1 << DirPosZ)) == 0)
                {
                    TryPropagateBlock(x, y, z + 1, currentBlock, 1 << DirNegZ, ref queue, ref lightData, ref chunkData, ref stateTable);
                }
            }
        }

        /// <summary>Attempts to propagate block light into a single neighbor voxel with filter attenuation.</summary>
        public static void TryPropagateBlock(int nx, int ny, int nz, byte sourceBlock,
            int neighborSkipMask, ref NativeQueue<int> queue,
            ref NativeArray<byte> lightData, ref NativeArray<StateId> chunkData,
            ref NativeArray<BlockStateCompact> stateTable)
        {
            if (nx < 0 || nx >= ChunkConstants.Size ||
                ny < 0 || ny >= ChunkConstants.Size ||
                nz < 0 || nz >= ChunkConstants.Size)
            {
                return;
            }

            int neighborIndex = ChunkData.GetIndex(nx, ny, nz);
            StateId neighborState = chunkData[neighborIndex];
            BlockStateCompact neighborBlockState = stateTable[neighborState.Value];

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

            byte neighborPacked = lightData[neighborIndex];
            byte neighborBlockLight = LightUtils.GetBlockLight(neighborPacked);

            if ((byte)newBlock > neighborBlockLight)
            {
                byte neighborSun = LightUtils.GetSunLight(neighborPacked);
                lightData[neighborIndex] = LightUtils.Pack(neighborSun, (byte)newBlock);
                queue.Enqueue(neighborIndex | (newBlock << LevelShift) | (neighborSkipMask << SkipShift));
            }
        }

        /// <summary>Scans all 6 chunk faces and collects voxels with light > 1 for cross-chunk propagation.</summary>
        public static void CollectBorderLightLeaks(
            ref NativeArray<byte> lightData,
            ref NativeList<NativeBorderLightEntry> output)
        {
            if (!output.IsCreated)
            {
                return;
            }

            int lastIdx = ChunkConstants.Size - 1;

            for (int a = 0; a < ChunkConstants.Size; a++)
            {
                for (int b = 0; b < ChunkConstants.Size; b++)
                {
                    // +X face (x=31)
                    CollectBorderVoxel(lastIdx, a, b, new int3(lastIdx, a, b), 0, ref lightData, ref output);
                    // -X face (x=0)
                    CollectBorderVoxel(0, a, b, new int3(0, a, b), 1, ref lightData, ref output);
                    // +Y face (y=31)
                    CollectBorderVoxel(a, lastIdx, b, new int3(a, lastIdx, b), 2, ref lightData, ref output);
                    // -Y face (y=0)
                    CollectBorderVoxel(a, 0, b, new int3(a, 0, b), 3, ref lightData, ref output);
                    // +Z face (z=31)
                    CollectBorderVoxel(a, b, lastIdx, new int3(a, b, lastIdx), 4, ref lightData, ref output);
                    // -Z face (z=0)
                    CollectBorderVoxel(a, b, 0, new int3(a, b, 0), 5, ref lightData, ref output);
                }
            }
        }

        /// <summary>Adds a border light entry if the voxel at (x, y, z) has sun or block light above 1.</summary>
        private static void CollectBorderVoxel(int x, int y, int z, int3 localPos, byte face,
            ref NativeArray<byte> lightData, ref NativeList<NativeBorderLightEntry> output)
        {
            int index = ChunkData.GetIndex(x, y, z);
            byte packed = lightData[index];
            byte sun = LightUtils.GetSunLight(packed);
            byte block = LightUtils.GetBlockLight(packed);

            if (sun > 1 || block > 1)
            {
                output.Add(new NativeBorderLightEntry
                {
                    LocalPosition = localPos,
                    PackedLight = packed,
                    Face = face,
                });
            }
        }
    }
}