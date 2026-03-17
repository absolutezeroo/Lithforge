using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Lithforge.Voxel.Liquid
{
    /// <summary>
    /// Burst-compiled cellular automaton for liquid simulation.
    /// Processes one chunk per Execute() call.
    ///
    /// Algorithm (per active voxel):
    ///   1. Check settled flag — skip if settled
    ///   2. Resolve new state: check above for falling water, count source neighbors,
    ///      find max neighbor level
    ///   3. Try to flow down first (highest priority)
    ///   4. If on solid surface, run BFS flow-direction search to find nearest drop-off
    ///   5. Spread horizontally toward shortest drop-off path
    ///   6. Check infinite-source rule
    ///   7. Emit LiquidChunkEdit for any changes
    ///   8. Update settled flags
    ///
    /// Ghost cells: 6 slabs of 1024 bytes provide read-only neighbor boundary data.
    /// Cross-boundary edits are output with ChunkOffset != (0,0,0).
    /// </summary>
    [BurstCompile]
    public struct LiquidSimJob : IJob
    {
        public NativeArray<byte> LiquidData;

        [ReadOnly] public NativeArray<StateId> BlockData;
        [ReadOnly] public NativeArray<BlockStateCompact> StateTable;
        [ReadOnly] public NativeArray<int> InputActiveSet;

        [ReadOnly] public NativeArray<byte> GhostPosX;
        [ReadOnly] public NativeArray<byte> GhostNegX;
        [ReadOnly] public NativeArray<byte> GhostPosY;
        [ReadOnly] public NativeArray<byte> GhostNegY;
        [ReadOnly] public NativeArray<byte> GhostPosZ;
        [ReadOnly] public NativeArray<byte> GhostNegZ;

        public NativeArray<byte> BfsVisited;

        public LiquidJobConfig Config;

        public NativeList<LiquidChunkEdit> OutputEdits;
        public NativeList<int> OutputActiveSet;

        private const int Size = ChunkConstants.Size;
        private const int SizeSq = ChunkConstants.SizeSquared;

        public void Execute()
        {
            for (int i = 0; i < InputActiveSet.Length; i++)
            {
                int flatIndex = InputActiveSet[i];
                ProcessVoxel(flatIndex);
            }
        }

        private void ProcessVoxel(int flatIndex)
        {
            int y = flatIndex / SizeSq;
            int remainder = flatIndex - (y * SizeSq);
            int z = remainder / Size;
            int x = remainder - (z * Size);

            byte cell = LiquidData[flatIndex];

            if (LiquidCell.IsSettled(cell))
            {
                return;
            }

            if (LiquidCell.IsEmpty(cell))
            {
                ProcessEmptyVoxel(x, y, z, flatIndex);
                return;
            }

            bool anyChange = false;
            byte effectiveLevel = LiquidCell.GetEffectiveLevel(cell);
            bool isSource = LiquidCell.IsSource(cell);

            // Phase 1: Try to flow down
            if (y > 0)
            {
                int belowIndex = flatIndex - SizeSq;
                byte belowCell = LiquidData[belowIndex];

                if (!IsSolidBlock(belowIndex))
                {
                    byte belowEffective = LiquidCell.GetEffectiveLevel(belowCell);

                    if (LiquidCell.IsEmpty(belowCell) || (!LiquidCell.IsSource(belowCell) && belowEffective < LiquidCell.MaxFlowingLevel))
                    {
                        byte newBelowCell = LiquidCell.MakeFlowing(LiquidCell.MaxFlowingLevel);
                        WriteLiquidAndEmitEdit(x, y - 1, z, belowIndex, newBelowCell, 0, 0, 0);
                        MarkNeighborsActive(x, y - 1, z);
                        anyChange = true;
                    }
                }
            }
            else
            {
                // y == 0: check ghost slab below
                byte belowGhost = GhostNegY[z * Size + x];

                if (LiquidCell.IsEmpty(belowGhost) || (!LiquidCell.IsSource(belowGhost) && LiquidCell.GetEffectiveLevel(belowGhost) < LiquidCell.MaxFlowingLevel))
                {
                    int belowFlatIndex = (Size - 1) * SizeSq + z * Size + x;
                    EmitCrossBoundaryEdit(belowFlatIndex, 0, -1, 0, LiquidCell.MakeFlowing(LiquidCell.MaxFlowingLevel));
                    anyChange = true;
                }
            }

            // Phase 2: Horizontal spread (only if source or not fully draining)
            if (isSource || effectiveLevel > 1)
            {
                byte newLevel = isSource ? LiquidCell.MaxFlowingLevel : (byte)(effectiveLevel - 1);

                if (newLevel >= 1)
                {
                    byte flowDirMask = FindFlowDirections(x, y, z);
                    byte spreadMask = flowDirMask != 0 ? flowDirMask : (byte)0x0F;

                    anyChange |= SpreadHorizontal(x, y, z, newLevel, spreadMask);
                }
            }

            // Phase 3: Infinite source rule
            if (!isSource)
            {
                int sourceCount = CountHorizontalSourceNeighbors(x, y, z);

                if (sourceCount >= Config.SourceNeighborThreshold)
                {
                    bool belowIsSolidOrSource = false;

                    if (y > 0)
                    {
                        int belowIndex = flatIndex - SizeSq;
                        belowIsSolidOrSource = IsSolidBlock(belowIndex) || LiquidCell.IsSource(LiquidData[belowIndex]);
                    }
                    else
                    {
                        byte belowGhost = GhostNegY[z * Size + x];
                        belowIsSolidOrSource = IsSolidBlock_Ghost(GhostNegY, z * Size + x) || LiquidCell.IsSource(belowGhost);
                    }

                    if (belowIsSolidOrSource)
                    {
                        WriteLiquidAndEmitEdit(x, y, z, flatIndex, LiquidCell.MakeSource(), 0, 0, 0);
                        MarkNeighborsActive(x, y, z);
                        anyChange = true;
                    }
                }
            }

            // Phase 4: Self-drain check (non-source only)
            if (!isSource && !anyChange)
            {
                byte maxNeighborLevel = GetMaxNeighborEffectiveLevel(x, y, z);
                bool hasAboveFluid = false;

                if (y < Size - 1)
                {
                    hasAboveFluid = LiquidCell.HasLiquid(LiquidData[flatIndex + SizeSq]);
                }
                else
                {
                    hasAboveFluid = LiquidCell.HasLiquid(GhostPosY[z * Size + x]);
                }

                byte expectedLevel = 0;

                if (hasAboveFluid)
                {
                    expectedLevel = LiquidCell.MaxFlowingLevel;
                }
                else if (maxNeighborLevel > 1)
                {
                    expectedLevel = (byte)(maxNeighborLevel - 1);
                }

                if (expectedLevel != effectiveLevel)
                {
                    if (expectedLevel == 0)
                    {
                        WriteLiquidAndEmitEdit(x, y, z, flatIndex, LiquidCell.Empty, 0, 0, 0);
                    }
                    else
                    {
                        WriteLiquidAndEmitEdit(x, y, z, flatIndex, LiquidCell.MakeFlowing(expectedLevel), 0, 0, 0);
                    }

                    MarkNeighborsActive(x, y, z);
                    anyChange = true;
                }
            }

            // Phase 5: Settled flag
            if (!anyChange)
            {
                LiquidData[flatIndex] = LiquidCell.SetSettled(cell, true);
            }
            else
            {
                OutputActiveSet.Add(flatIndex);
            }
        }

        private void ProcessEmptyVoxel(int x, int y, int z, int flatIndex)
        {
            // Check if this empty voxel should receive flow from above
            bool hasAboveFluid = false;

            if (y < Size - 1)
            {
                hasAboveFluid = LiquidCell.HasLiquid(LiquidData[flatIndex + SizeSq]);
            }
            else
            {
                hasAboveFluid = LiquidCell.HasLiquid(GhostPosY[z * Size + x]);
            }

            if (hasAboveFluid && !IsSolidBlock(flatIndex))
            {
                WriteLiquidAndEmitEdit(x, y, z, flatIndex, LiquidCell.MakeFlowing(LiquidCell.MaxFlowingLevel), 0, 0, 0);
                MarkNeighborsActive(x, y, z);
                OutputActiveSet.Add(flatIndex);
                return;
            }

            // Check if should receive horizontal flow from any neighbor
            byte maxNeighborLevel = GetMaxNeighborEffectiveLevel(x, y, z);

            if (maxNeighborLevel > 1 && !IsSolidBlock(flatIndex))
            {
                byte newLevel = (byte)(maxNeighborLevel - 1);
                WriteLiquidAndEmitEdit(x, y, z, flatIndex, LiquidCell.MakeFlowing(newLevel), 0, 0, 0);
                MarkNeighborsActive(x, y, z);
                OutputActiveSet.Add(flatIndex);
            }
        }

        private bool SpreadHorizontal(int x, int y, int z, byte newLevel, byte dirMask)
        {
            bool changed = false;

            if ((dirMask & 0x01) != 0)
            {
                changed |= TrySpreadTo(x + 1, y, z, newLevel, 1, 0, 0);
            }

            if ((dirMask & 0x02) != 0)
            {
                changed |= TrySpreadTo(x - 1, y, z, newLevel, -1, 0, 0);
            }

            if ((dirMask & 0x04) != 0)
            {
                changed |= TrySpreadTo(x, y, z + 1, newLevel, 0, 0, 1);
            }

            if ((dirMask & 0x08) != 0)
            {
                changed |= TrySpreadTo(x, y, z - 1, newLevel, 0, 0, -1);
            }

            return changed;
        }

        private bool TrySpreadTo(int nx, int ny, int nz, byte newLevel, int ox, int oy, int oz)
        {
            if (nx >= 0 && nx < Size && nz >= 0 && nz < Size)
            {
                int neighborIndex = ny * SizeSq + nz * Size + nx;

                if (IsSolidBlock(neighborIndex))
                {
                    return false;
                }

                byte neighborCell = LiquidData[neighborIndex];
                byte neighborEffective = LiquidCell.GetEffectiveLevel(neighborCell);

                if (LiquidCell.IsSource(neighborCell))
                {
                    return false;
                }

                if (LiquidCell.IsEmpty(neighborCell) || neighborEffective < newLevel)
                {
                    WriteLiquidAndEmitEdit(nx, ny, nz, neighborIndex, LiquidCell.MakeFlowing(newLevel), 0, 0, 0);
                    MarkNeighborsActive(nx, ny, nz);
                    return true;
                }

                return false;
            }
            else
            {
                // Cross-boundary: emit edit for neighbor chunk
                int targetX = nx;
                int targetZ = nz;
                int chunkOffsetX = 0;
                int chunkOffsetZ = 0;

                if (nx < 0)
                {
                    targetX = Size - 1;
                    chunkOffsetX = -1;
                }
                else if (nx >= Size)
                {
                    targetX = 0;
                    chunkOffsetX = 1;
                }

                if (nz < 0)
                {
                    targetZ = Size - 1;
                    chunkOffsetZ = -1;
                }
                else if (nz >= Size)
                {
                    targetZ = 0;
                    chunkOffsetZ = 1;
                }

                int targetIndex = ny * SizeSq + targetZ * Size + targetX;
                EmitCrossBoundaryEdit(targetIndex, chunkOffsetX, 0, chunkOffsetZ, LiquidCell.MakeFlowing(newLevel));
                return true;
            }
        }

        private unsafe byte FindFlowDirections(int x, int y, int z)
        {
            // BFS to find nearest drop-off within FlowSearchRadius blocks.
            // Returns 4-bit direction mask: bit 0=+X, 1=-X, 2=+Z, 3=-Z
            int radius = Config.FlowSearchRadius;
            const int StackCap = 128;

            int* stack = stackalloc int[StackCap];
            int head = 0;
            int tail = 0;

            // Clear visited bitset (32x32 = 1024 bits = 128 bytes)
            for (int bi = 0; bi < 128; bi++)
            {
                BfsVisited[bi] = 0;
            }

            // Mark origin as visited
            int originBit = z * Size + x;
            BfsVisited[originBit >> 3] = (byte)(BfsVisited[originBit >> 3] | (1 << (originBit & 7)));

            byte foundDirMask = 0;
            int bestDistance = 1000;

            // Seed 4 directions
            TryBfsPush(x + 1, z, y, 1, 0x01, radius, stack, head, ref tail, StackCap);
            TryBfsPush(x - 1, z, y, 1, 0x02, radius, stack, head, ref tail, StackCap);
            TryBfsPush(x, z + 1, y, 1, 0x04, radius, stack, head, ref tail, StackCap);
            TryBfsPush(x, z - 1, y, 1, 0x08, radius, stack, head, ref tail, StackCap);

            while (head < tail)
            {
                int entry = stack[head & (StackCap - 1)];
                head++;

                int nx = (entry & 0xFF);
                int nz = ((entry >> 8) & 0xFF);
                int depth = (entry >> 16) & 0xFF;
                byte dirMask = (byte)((entry >> 24) & 0xFF);

                if (depth > bestDistance)
                {
                    continue;
                }

                // Check if there is a drop-off: air below this position
                bool hasDropOff = false;

                if (nx >= 0 && nx < Size && nz >= 0 && nz < Size)
                {
                    if (y > 0)
                    {
                        int belowIndex = (y - 1) * SizeSq + nz * Size + nx;
                        hasDropOff = !IsSolidBlock(belowIndex) && LiquidCell.IsEmpty(LiquidData[belowIndex]);
                    }
                }

                if (hasDropOff)
                {
                    if (depth < bestDistance)
                    {
                        bestDistance = depth;
                        foundDirMask = dirMask;
                    }
                    else if (depth == bestDistance)
                    {
                        foundDirMask |= dirMask;
                    }

                    continue;
                }

                if (depth >= radius)
                {
                    continue;
                }

                TryBfsPush(nx + 1, nz, y, depth + 1, dirMask, radius, stack, head, ref tail, StackCap);
                TryBfsPush(nx - 1, nz, y, depth + 1, dirMask, radius, stack, head, ref tail, StackCap);
                TryBfsPush(nx, nz + 1, y, depth + 1, dirMask, radius, stack, head, ref tail, StackCap);
                TryBfsPush(nx, nz - 1, y, depth + 1, dirMask, radius, stack, head, ref tail, StackCap);
            }

            return foundDirMask;
        }

        private unsafe void TryBfsPush(int nx, int nz, int y, int depth, byte dirMask, int radius,
            int* stack, int head, ref int tail, int cap)
        {
            if (nx < 0 || nx >= Size || nz < 0 || nz >= Size)
            {
                return;
            }

            // Visited check
            int bitIdx = nz * Size + nx;
            int byteIdx = bitIdx >> 3;
            int bitMask = 1 << (bitIdx & 7);

            if ((BfsVisited[byteIdx] & bitMask) != 0)
            {
                return;
            }

            BfsVisited[byteIdx] = (byte)(BfsVisited[byteIdx] | bitMask);

            // Must be passable (not solid)
            int flatIndex = y * SizeSq + nz * Size + nx;

            if (IsSolidBlock(flatIndex))
            {
                return;
            }

            if (tail - head >= cap)
            {
                return;
            }

            int packed = nx | (nz << 8) | (depth << 16) | (dirMask << 24);
            stack[tail & (cap - 1)] = packed;
            tail++;
        }

        private bool IsSolidBlock(int flatIndex)
        {
            StateId stateId = BlockData[flatIndex];
            BlockStateCompact compact = StateTable[stateId.Value];

            return compact.CollisionShape != 0 && !compact.IsFluid;
        }

        private bool IsSolidBlock_Ghost(NativeArray<byte> ghostSlab, int slabIndex)
        {
            // Ghost slabs only contain liquid data, not block data.
            // For cross-boundary solidity, we conservatively treat unloaded/ghost
            // as non-solid to allow flow. The main-thread scheduler validates
            // cross-boundary edits before applying.
            return false;
        }

        private int CountHorizontalSourceNeighbors(int x, int y, int z)
        {
            int count = 0;

            // +X
            if (x < Size - 1)
            {
                if (LiquidCell.IsSource(LiquidData[y * SizeSq + z * Size + x + 1]))
                {
                    count++;
                }
            }
            else
            {
                if (LiquidCell.IsSource(GhostPosX[y * Size + z]))
                {
                    count++;
                }
            }

            // -X
            if (x > 0)
            {
                if (LiquidCell.IsSource(LiquidData[y * SizeSq + z * Size + x - 1]))
                {
                    count++;
                }
            }
            else
            {
                if (LiquidCell.IsSource(GhostNegX[y * Size + z]))
                {
                    count++;
                }
            }

            // +Z
            if (z < Size - 1)
            {
                if (LiquidCell.IsSource(LiquidData[y * SizeSq + (z + 1) * Size + x]))
                {
                    count++;
                }
            }
            else
            {
                if (LiquidCell.IsSource(GhostPosZ[y * Size + x]))
                {
                    count++;
                }
            }

            // -Z
            if (z > 0)
            {
                if (LiquidCell.IsSource(LiquidData[y * SizeSq + (z - 1) * Size + x]))
                {
                    count++;
                }
            }
            else
            {
                if (LiquidCell.IsSource(GhostNegZ[y * Size + x]))
                {
                    count++;
                }
            }

            return count;
        }

        private byte GetMaxNeighborEffectiveLevel(int x, int y, int z)
        {
            byte maxLevel = 0;

            // +X
            if (x < Size - 1)
            {
                byte l = LiquidCell.GetEffectiveLevel(LiquidData[y * SizeSq + z * Size + x + 1]);

                if (l > maxLevel)
                {
                    maxLevel = l;
                }
            }
            else
            {
                byte l = LiquidCell.GetEffectiveLevel(GhostPosX[y * Size + z]);

                if (l > maxLevel)
                {
                    maxLevel = l;
                }
            }

            // -X
            if (x > 0)
            {
                byte l = LiquidCell.GetEffectiveLevel(LiquidData[y * SizeSq + z * Size + x - 1]);

                if (l > maxLevel)
                {
                    maxLevel = l;
                }
            }
            else
            {
                byte l = LiquidCell.GetEffectiveLevel(GhostNegX[y * Size + z]);

                if (l > maxLevel)
                {
                    maxLevel = l;
                }
            }

            // +Z
            if (z < Size - 1)
            {
                byte l = LiquidCell.GetEffectiveLevel(LiquidData[y * SizeSq + (z + 1) * Size + x]);

                if (l > maxLevel)
                {
                    maxLevel = l;
                }
            }
            else
            {
                byte l = LiquidCell.GetEffectiveLevel(GhostPosZ[y * Size + x]);

                if (l > maxLevel)
                {
                    maxLevel = l;
                }
            }

            // -Z
            if (z > 0)
            {
                byte l = LiquidCell.GetEffectiveLevel(LiquidData[y * SizeSq + (z - 1) * Size + x]);

                if (l > maxLevel)
                {
                    maxLevel = l;
                }
            }
            else
            {
                byte l = LiquidCell.GetEffectiveLevel(GhostNegZ[y * Size + x]);

                if (l > maxLevel)
                {
                    maxLevel = l;
                }
            }

            return maxLevel;
        }

        private void WriteLiquidAndEmitEdit(int x, int y, int z, int flatIndex, byte newCell,
            int chunkOffX, int chunkOffY, int chunkOffZ)
        {
            byte oldCell = LiquidData[flatIndex];

            if (oldCell == newCell)
            {
                return;
            }

            LiquidData[flatIndex] = newCell;

            // Map liquid cell to StateId
            StateId newStateId;

            if (LiquidCell.IsEmpty(newCell))
            {
                newStateId = StateId.Air;
            }
            else if (LiquidCell.IsSource(newCell))
            {
                newStateId = new StateId(Config.SourceStateId);
            }
            else
            {
                byte level = LiquidCell.GetLevel(newCell);
                newStateId = new StateId((ushort)(Config.SourceStateId + level));
            }

            OutputEdits.Add(new LiquidChunkEdit
            {
                FlatIndex = flatIndex,
                ChunkOffset = new Unity.Mathematics.int3(chunkOffX, chunkOffY, chunkOffZ),
                NewLiquidCell = newCell,
                NewStateId = newStateId,
            });
        }

        private void EmitCrossBoundaryEdit(int targetFlatIndex, int chunkOffX, int chunkOffY, int chunkOffZ, byte newCell)
        {
            StateId newStateId;

            if (LiquidCell.IsEmpty(newCell))
            {
                newStateId = StateId.Air;
            }
            else if (LiquidCell.IsSource(newCell))
            {
                newStateId = new StateId(Config.SourceStateId);
            }
            else
            {
                byte level = LiquidCell.GetLevel(newCell);
                newStateId = new StateId((ushort)(Config.SourceStateId + level));
            }

            OutputEdits.Add(new LiquidChunkEdit
            {
                FlatIndex = targetFlatIndex,
                ChunkOffset = new Unity.Mathematics.int3(chunkOffX, chunkOffY, chunkOffZ),
                NewLiquidCell = newCell,
                NewStateId = newStateId,
            });
        }

        private void MarkNeighborsActive(int x, int y, int z)
        {
            // Mark the 6 neighbors of (x,y,z) as active for next tick
            // by clearing their settled flags
            TryClearSettled(x + 1, y, z);
            TryClearSettled(x - 1, y, z);
            TryClearSettled(x, y + 1, z);
            TryClearSettled(x, y - 1, z);
            TryClearSettled(x, y, z + 1);
            TryClearSettled(x, y, z - 1);
        }

        private void TryClearSettled(int x, int y, int z)
        {
            if (x < 0 || x >= Size || y < 0 || y >= Size || z < 0 || z >= Size)
            {
                return;
            }

            int idx = y * SizeSq + z * Size + x;
            byte cell = LiquidData[idx];

            if (LiquidCell.IsSettled(cell))
            {
                LiquidData[idx] = LiquidCell.ClearSettled(cell);
                OutputActiveSet.Add(idx);
            }
        }
    }
}
