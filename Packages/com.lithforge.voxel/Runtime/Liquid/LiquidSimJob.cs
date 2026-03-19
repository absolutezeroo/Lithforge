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
        /// <summary>Per-voxel liquid cell data, read and written in-place.</summary>
        public NativeArray<byte> LiquidData;

        /// <summary>Block state IDs for solid-block checks.</summary>
        [ReadOnly] public NativeArray<StateId> BlockData;

        /// <summary>Block state compact table for collision shape lookups.</summary>
        [ReadOnly] public NativeArray<BlockStateCompact> StateTable;

        /// <summary>Flat indices of voxels to process this tick.</summary>
        [ReadOnly] public NativeArray<int> InputActiveSet;

        /// <summary>Neighbor liquid ghost slab for +X boundary reads.</summary>
        [ReadOnly] public NativeArray<byte> GhostPosX;

        /// <summary>Neighbor liquid ghost slab for -X boundary reads.</summary>
        [ReadOnly] public NativeArray<byte> GhostNegX;

        /// <summary>Neighbor liquid ghost slab for +Y boundary reads.</summary>
        [ReadOnly] public NativeArray<byte> GhostPosY;

        /// <summary>Neighbor liquid ghost slab for -Y boundary reads.</summary>
        [ReadOnly] public NativeArray<byte> GhostNegY;

        /// <summary>Neighbor liquid ghost slab for +Z boundary reads.</summary>
        [ReadOnly] public NativeArray<byte> GhostPosZ;

        /// <summary>Neighbor liquid ghost slab for -Z boundary reads.</summary>
        [ReadOnly] public NativeArray<byte> GhostNegZ;

        /// <summary>Neighbor block solidity ghost slab for +X. Index: [y * Size + z], 1 = solid.</summary>
        [ReadOnly] public NativeArray<byte> GhostBlockSolidPosX;

        /// <summary>Neighbor block solidity ghost slab for -X.</summary>
        [ReadOnly] public NativeArray<byte> GhostBlockSolidNegX;

        /// <summary>Neighbor block solidity ghost slab for +Z. Index: [y * Size + x].</summary>
        [ReadOnly] public NativeArray<byte> GhostBlockSolidPosZ;

        /// <summary>Neighbor block solidity ghost slab for -Z.</summary>
        [ReadOnly] public NativeArray<byte> GhostBlockSolidNegZ;

        /// <summary>Per-fluid configuration (water or lava parameters).</summary>
        public LiquidJobConfig Config;

        /// <summary>Output list of voxel edits to apply on the main thread.</summary>
        public NativeList<LiquidChunkEdit> OutputEdits;

        /// <summary>Output list of flat indices remaining active for next tick.</summary>
        public NativeList<int> OutputActiveSet;

        private const int Size = ChunkConstants.Size;
        private const int SizeSq = ChunkConstants.SizeSquared;

        /// <summary>Iterates over the active set and processes each voxel's liquid state.</summary>
        public void Execute()
        {
            for (int i = 0; i < InputActiveSet.Length; i++)
            {
                int flatIndex = InputActiveSet[i];
                ProcessVoxel(flatIndex);
            }
        }

        /// <summary>Pulls the new liquid state for a single voxel from its neighbors and emits edits.</summary>
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
                // No change — settle, but wake empty neighbors so they can pull flow
                LiquidData[flatIndex] = LiquidCell.SetSettled(cell, true);

                if (LiquidCell.HasLiquid(newCell))
                {
                    WakeEmptyNeighbors(x, y, z);
                }
            }
            else
            {
                // Changed — write, emit, wake neighbors
                WriteLiquidAndEmitEdit(x, y, z, flatIndex, newCell, 0, 0, 0);
                MarkNeighborsActive(x, y, z);
                OutputActiveSet.Add(flatIndex);
            }

            // --- Gravity push: wake below only if it would actually change ---
            if (LiquidCell.HasLiquid(newCell))
            {
                if (y > 0)
                {
                    int belowIndex = flatIndex - SizeSq;

                    if (!IsSolidBlock(belowIndex))
                    {
                        byte belowCell = LiquidData[belowIndex];

                        if (LiquidCell.IsEmpty(belowCell) ||
                            (!LiquidCell.IsSource(belowCell) &&
                             LiquidCell.GetEffectiveLevel(belowCell) < LiquidCell.MaxFlowingLevel))
                        {
                            TryClearSettled(x, y - 1, z);
                        }
                    }
                }
                else
                {
                    // y == 0: check ghost slab before emitting cross-boundary edit
                    byte belowGhost = GhostNegY[z * Size + x];

                    if (LiquidCell.IsEmpty(belowGhost) ||
                        (!LiquidCell.IsSource(belowGhost) &&
                         LiquidCell.GetEffectiveLevel(belowGhost) < LiquidCell.MaxFlowingLevel))
                    {
                        int belowFlatIndex = (Size - 1) * SizeSq + z * Size + x;
                        EmitCrossBoundaryEdit(belowFlatIndex, 0, -1, 0, LiquidCell.MakeFlowing(LiquidCell.MaxFlowingLevel));
                    }
                }
            }
        }

        /// <summary>Returns true if the block at the given flat index has collision and is not a fluid.</summary>
        private bool IsSolidBlock(int flatIndex)
        {
            StateId stateId = BlockData[flatIndex];
            BlockStateCompact compact = StateTable[stateId.Value];

            return compact.CollisionShape != 0 && !compact.IsFluid;
        }

        /// <summary>Placeholder for ghost-slab block solidity check on -Y face. Currently always returns false.</summary>
        private bool IsSolidBlock_Ghost(NativeArray<byte> ghostSlab, int slabIndex)
        {
            // TODO: add -Y block solidity ghost for chunk-bottom source rule.
            // Currently treats below as non-solid (conservative: source rule won't fire,
            // water stays flowing instead of becoming source — slightly wrong but rare).
            return false;
        }

        /// <summary>Checks block solidity via horizontal ghost slabs for out-of-bounds neighbor coordinates.</summary>
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

        /// <summary>Checks if a neighbor liquid cell is empty via ghost slabs for out-of-bounds coordinates.</summary>
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

        /// <summary>Counts the number of horizontal neighbors that are source blocks (for infinite source rule).</summary>
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

        /// <summary>Returns the highest effective liquid level among the 4 horizontal neighbors.</summary>
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

        /// <summary>Writes the new liquid cell to LiquidData and emits a LiquidChunkEdit if changed.</summary>
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

        /// <summary>Emits a cross-chunk boundary edit for a neighbor chunk's voxel.</summary>
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

        /// <summary>Clears the settled flag on all 6 neighbors to wake them for next tick.</summary>
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

        /// <summary>Clears the settled flag on a neighbor voxel if in-bounds and adds it to the active set.</summary>
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
            else if (LiquidCell.IsEmpty(cell) && !IsSolidBlock(idx))
            {
                OutputActiveSet.Add(idx);
            }
        }

        /// <summary>Adds empty non-solid neighbors to the active set so they can pull flow.</summary>
        private void WakeEmptyNeighbors(int x, int y, int z)
        {
            TryWakeEmpty(x + 1, y, z);
            TryWakeEmpty(x - 1, y, z);
            TryWakeEmpty(x, y + 1, z);
            TryWakeEmpty(x, y - 1, z);
            TryWakeEmpty(x, y, z + 1);
            TryWakeEmpty(x, y, z - 1);
        }

        /// <summary>Adds a single neighbor to the active set if it is empty and not solid.</summary>
        private void TryWakeEmpty(int x, int y, int z)
        {
            if (x < 0 || x >= Size || y < 0 || y >= Size || z < 0 || z >= Size)
            {
                return;
            }

            int idx = y * SizeSq + z * Size + x;
            byte cell = LiquidData[idx];

            if (LiquidCell.IsEmpty(cell) && !IsSolidBlock(idx))
            {
                OutputActiveSet.Add(idx);
            }
        }
    }
}
