using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Lithforge.Voxel.Liquid
{
    /// <summary>
    /// Burst-compiled cellular automaton for liquid simulation (pull model).
    /// Processes one chunk per Execute() call.
    ///
    /// Algorithm (per active voxel — each voxel pulls its new state from neighbors):
    ///   1. Check settled flag — skip if settled
    ///   2. Rule A: Sources persist (wasSource → remain source)
    ///   3. Rule B: Infinite source promotion (≥2 horizontal source neighbors + solid/source below)
    ///   4. Rule C: Compute flowing level from neighbors:
    ///      - Above has fluid → level 7 (falling water)
    ///      - Otherwise → max(horizontal neighbor effective level) - 1
    ///      - If ≤0 → drain to empty
    ///   5. Compare old vs new (ignoring settled bit): same → settle, different → write + emit + wake
    ///   6. If has liquid, wake below neighbor (gravity push)
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

        [ReadOnly] public NativeArray<byte> GhostBlockSolidPosX; // [y * Size + z], 1 = solid
        [ReadOnly] public NativeArray<byte> GhostBlockSolidNegX;
        [ReadOnly] public NativeArray<byte> GhostBlockSolidPosZ; // [y * Size + x]
        [ReadOnly] public NativeArray<byte> GhostBlockSolidNegZ;

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

            bool wasSource = LiquidCell.IsSource(cell);
            bool wasSolid = IsSolidBlock(flatIndex);

            // Solid blocks cannot hold liquid — skip entirely
            if (wasSolid)
            {
                if (LiquidCell.HasLiquid(cell))
                {
                    WriteLiquidAndEmitEdit(x, y, z, flatIndex, LiquidCell.Empty, 0, 0, 0);
                    MarkNeighborsActive(x, y, z);
                    OutputActiveSet.Add(flatIndex);
                }

                return;
            }

            // --- Pull: determine what this cell SHOULD be ---

            byte newCell = LiquidCell.Empty;

            // Rule A: Sources persist
            if (wasSource)
            {
                newCell = LiquidCell.MakeSource();
            }
            else
            {
                // Rule B: Infinite source promotion
                int sourceCount = CountHorizontalSourceNeighbors(x, y, z);
                bool promoted = false;

                if (sourceCount >= Config.SourceNeighborThreshold)
                {
                    bool belowIsSolidOrSource = false;

                    if (y > 0)
                    {
                        int belowIndex = flatIndex - SizeSq;
                        belowIsSolidOrSource = IsSolidBlock(belowIndex) ||
                            LiquidCell.IsSource(LiquidData[belowIndex]);
                    }
                    else
                    {
                        byte belowGhost = GhostNegY[z * Size + x];
                        belowIsSolidOrSource = IsSolidBlock_Ghost(GhostNegY, z * Size + x) ||
                            LiquidCell.IsSource(belowGhost);
                    }

                    if (belowIsSolidOrSource)
                    {
                        newCell = LiquidCell.MakeSource();
                        promoted = true;
                    }
                }

                // Rule C: Flowing level from neighbors
                if (!promoted)
                {
                    bool hasAboveFluid = false;

                    if (y < Size - 1)
                    {
                        hasAboveFluid = LiquidCell.HasLiquid(LiquidData[flatIndex + SizeSq]);
                    }
                    else
                    {
                        hasAboveFluid = LiquidCell.HasLiquid(GhostPosY[z * Size + x]);
                    }

                    if (hasAboveFluid)
                    {
                        newCell = LiquidCell.MakeFlowing(LiquidCell.MaxFlowingLevel);
                    }
                    else
                    {
                        byte maxNeighborLevel = GetMaxNeighborEffectiveLevel(x, y, z);

                        if (maxNeighborLevel > 1)
                        {
                            newCell = LiquidCell.MakeFlowing((byte)(maxNeighborLevel - 1));
                        }
                        else
                        {
                            newCell = LiquidCell.Empty;
                        }
                    }
                }
            }

            // --- Compare old vs new (ignoring settled bit) ---
            byte oldStripped = LiquidCell.ClearSettled(cell);

            if (oldStripped == newCell)
            {
                // No change — settle
                LiquidData[flatIndex] = LiquidCell.SetSettled(cell, true);
            }
            else
            {
                // Changed — write, emit, wake neighbors
                WriteLiquidAndEmitEdit(x, y, z, flatIndex, newCell, 0, 0, 0);
                MarkNeighborsActive(x, y, z);
                OutputActiveSet.Add(flatIndex);
            }

            // --- Gravity push: if we have liquid, wake the cell below ---
            if (LiquidCell.HasLiquid(newCell))
            {
                if (y > 0)
                {
                    int belowIndex = flatIndex - SizeSq;

                    if (!IsSolidBlock(belowIndex))
                    {
                        byte belowCell = LiquidData[belowIndex];

                        if (LiquidCell.IsSettled(belowCell) || LiquidCell.IsEmpty(belowCell))
                        {
                            TryClearSettled(x, y - 1, z);

                            if (LiquidCell.IsEmpty(belowCell))
                            {
                                OutputActiveSet.Add(belowIndex);
                            }
                        }
                    }
                }
                else
                {
                    // y == 0: emit cross-boundary seed to wake below neighbor
                    int belowFlatIndex = (Size - 1) * SizeSq + z * Size + x;
                    EmitCrossBoundaryEdit(belowFlatIndex, 0, -1, 0, LiquidCell.MakeFlowing(LiquidCell.MaxFlowingLevel));
                }
            }
        }

        private bool IsSolidBlock(int flatIndex)
        {
            StateId stateId = BlockData[flatIndex];
            BlockStateCompact compact = StateTable[stateId.Value];

            return compact.CollisionShape != 0 && !compact.IsFluid;
        }

        private bool IsSolidBlock_Ghost(NativeArray<byte> ghostSlab, int slabIndex)
        {
            // TODO: add -Y block solidity ghost for chunk-bottom source rule.
            // Currently treats below as non-solid (conservative: source rule won't fire,
            // water stays flowing instead of becoming source — slightly wrong but rare).
            return false;
        }

        private bool IsSolidBlockGhost(int nx, int nz, int y)
        {
            if (nx < 0)
            {
                if (nz < 0 || nz >= Size)
                {
                    return true; // corner = treat as solid
                }

                return GhostBlockSolidNegX[y * Size + nz] != 0;
            }

            if (nx >= Size)
            {
                if (nz < 0 || nz >= Size)
                {
                    return true;
                }

                return GhostBlockSolidPosX[y * Size + nz] != 0;
            }

            if (nz < 0)
            {
                return GhostBlockSolidNegZ[y * Size + nx] != 0;
            }

            if (nz >= Size)
            {
                return GhostBlockSolidPosZ[y * Size + nx] != 0;
            }

            return false;
        }

        private bool IsLiquidEmptyGhost(int nx, int nz, int y)
        {
            if (nx < 0)
            {
                if (nz < 0 || nz >= Size)
                {
                    return false; // corner = assume not empty (conservative)
                }

                return LiquidCell.IsEmpty(GhostNegX[y * Size + nz]);
            }

            if (nx >= Size)
            {
                if (nz < 0 || nz >= Size)
                {
                    return false;
                }

                return LiquidCell.IsEmpty(GhostPosX[y * Size + nz]);
            }

            if (nz < 0)
            {
                return LiquidCell.IsEmpty(GhostNegZ[y * Size + nx]);
            }

            if (nz >= Size)
            {
                return LiquidCell.IsEmpty(GhostPosZ[y * Size + nx]);
            }

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
