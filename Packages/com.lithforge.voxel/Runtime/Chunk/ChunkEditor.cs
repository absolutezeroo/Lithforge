using System;
using System.Collections.Generic;

using Lithforge.Voxel.Block;

using Unity.Collections;
using Unity.Mathematics;

namespace Lithforge.Voxel.Chunk
{
    /// <summary>
    ///     Encapsulates block editing operations: reading/writing individual blocks,
    ///     deferred edits during meshing, border dirtying, and block entity events.
    ///     Internal helper owned by <see cref="ChunkManager" />.
    /// </summary>
    internal sealed class ChunkEditor
    {
        /// <summary>Delegate to retrieve a chunk by coordinate from the owning ChunkManager.</summary>
        private readonly Func<int3, ManagedChunk> _getChunk;

        /// <summary>Delegate to transition a chunk's state via the owning ChunkManager.</summary>
        private readonly Action<ManagedChunk, ChunkState> _setChunkState;

        /// <summary>Reusable list for collecting dirtied chunk coords during deferred edit application.</summary>
        private readonly List<int3> _deferredDirtiedCache = new();

        /// <summary>
        ///     NativeStateRegistry reference for checking HasBlockEntity flag during SetBlock.
        ///     Must be set after content pipeline completes.
        /// </summary>
        private NativeStateRegistry _nativeStateRegistry;

        /// <summary>
        ///     Called after any block change (both immediate and deferred paths).
        ///     Parameters: worldCoord, newStateId.
        ///     Used by <see cref="Network.ChunkDirtyTracker" /> for network delta sync.
        /// </summary>
        internal Action<int3, StateId> OnBlockChanged;

        /// <summary>
        ///     Called when a block with FlagHasBlockEntity is placed.
        ///     Parameters: chunkCoord, flatIndex, stateId.
        /// </summary>
        internal Action<int3, int, StateId> OnBlockEntityPlaced;

        /// <summary>
        ///     Called when a block with FlagHasBlockEntity is broken (replaced by air).
        ///     Parameters: chunkCoord, flatIndex, oldStateId.
        /// </summary>
        internal Action<int3, int, StateId> OnBlockEntityRemoved;

        /// <summary>Creates a ChunkEditor with delegates back to the owning ChunkManager.</summary>
        internal ChunkEditor(
            Func<int3, ManagedChunk> getChunk,
            Action<ManagedChunk, ChunkState> setChunkState)
        {
            _getChunk = getChunk;
            _setChunkState = setChunkState;
        }

        /// <summary>
        ///     Sets the NativeStateRegistry for block entity flag checks in SetBlock.
        ///     Must be called after content pipeline completes and before gameplay starts.
        /// </summary>
        internal void SetNativeStateRegistry(NativeStateRegistry nativeStateRegistry)
        {
            _nativeStateRegistry = nativeStateRegistry;
        }

        /// <summary>
        ///     Returns true if the block at the given world coordinate is in a loaded,
        ///     sufficiently-generated chunk. Returns false if the chunk is missing,
        ///     still generating, or has no data.
        /// </summary>
        internal bool IsBlockLoaded(int3 worldCoord)
        {
            int3 chunkCoord = ChunkManager.WorldToChunk(worldCoord);
            ManagedChunk chunk = _getChunk(chunkCoord);

            return chunk is
            {
                State: >= ChunkState.RelightPending,
                Data:
                {
                    IsCreated: true,
                },
            };
        }

        /// <summary>
        ///     Gets the StateId at a world-space block coordinate.
        ///     Returns StateId.Air if the chunk is not loaded or not yet generated.
        /// </summary>
        internal StateId GetBlock(int3 worldCoord)
        {
            int3 chunkCoord = ChunkManager.WorldToChunk(worldCoord);
            ManagedChunk chunk = _getChunk(chunkCoord);

            if (chunk == null || chunk.State < ChunkState.RelightPending || !chunk.Data.IsCreated)
            {
                return StateId.Air;
            }

            int localX = worldCoord.x - chunkCoord.x * ChunkConstants.Size;
            int localY = worldCoord.y - chunkCoord.y * ChunkConstants.Size;
            int localZ = worldCoord.z - chunkCoord.z * ChunkConstants.Size;
            int index = ChunkData.GetIndex(localX, localY, localZ);

            NativeArray<StateId> data = chunk.Data;

            return data[index];
        }

        /// <summary>
        ///     Sets the StateId at a world-space block coordinate.
        ///     Marks the chunk (and border-adjacent neighbors) for remeshing.
        ///     Sets the chunk to RelightPending so light is recalculated before remesh.
        ///     Does nothing if the chunk is not loaded or still generating.
        /// </summary>
        internal void SetBlock(int3 worldCoord, StateId state, List<int3> dirtiedChunks)
        {
            int3 chunkCoord = ChunkManager.WorldToChunk(worldCoord);
            ManagedChunk chunk = _getChunk(chunkCoord);

            if (chunk == null || chunk.State < ChunkState.RelightPending || !chunk.Data.IsCreated)
            {
                return;
            }

            int localX = worldCoord.x - chunkCoord.x * ChunkConstants.Size;
            int localY = worldCoord.y - chunkCoord.y * ChunkConstants.Size;
            int localZ = worldCoord.z - chunkCoord.z * ChunkConstants.Size;
            int index = ChunkData.GetIndex(localX, localY, localZ);

            chunk.IsDirty = true;
            chunk.HasPlayerEdit = true;
            chunk.NetworkVersion++;

            if (state.Value != 0)
            {
                chunk.IsAllAir = false;
            }

            // Check block entity flags for old and new states
            StateId oldState = chunk.Data[index];
            bool oldHasEntity = _nativeStateRegistry.States.IsCreated &&
                                oldState.Value < _nativeStateRegistry.States.Length &&
                                _nativeStateRegistry.States[oldState.Value].HasBlockEntity;
            bool newHasEntity = _nativeStateRegistry.States.IsCreated &&
                                state.Value < _nativeStateRegistry.States.Length &&
                                _nativeStateRegistry.States[state.Value].HasBlockEntity;

            if (chunk.State == ChunkState.Meshing)
            {
                // Defer the edit — do NOT write to ChunkData while the mesh job is
                // reading it. The edit will be applied in ApplyDeferredEdits() after
                // the job finishes. Block entity events are also deferred.
                chunk.DeferredEdits.Add(new DeferredEdit
                {
                    FlatIndex = index, OldState = oldState, NewState = state,
                });
            }
            else
            {
                // Complete any jobs that hold this chunk's Data before writing.
                // Light jobs read ChunkData as [ReadOnly].
                if (chunk.LightJobInFlight)
                {
                    chunk.ActiveJobHandle.Complete();
                }

                // LiquidSimJob reads BlockData as [ReadOnly].
                chunk.LiquidJobHandle.Complete();

                // Neighbor mesh jobs read this chunk's Data as border slices via
                // ExtractAllBordersJob. Complete them to release safety locks.
                for (int face = 0; face < 6; face++)
                {
                    ManagedChunk neighbor = chunk.Neighbors[face];

                    if (neighbor is
                        {
                            State: ChunkState.Meshing,
                        })
                    {
                        neighbor.ActiveJobHandle.Complete();
                    }
                }

                // Normal path: write immediately and trigger relight
                NativeArray<StateId> data = chunk.Data;
                data[index] = state;
                chunk.PendingEditIndices.Add(index);
                _setChunkState(chunk, ChunkState.RelightPending);

                // Fire block entity events only on the immediate path
                if (oldHasEntity && !newHasEntity)
                {
                    OnBlockEntityRemoved?.Invoke(chunkCoord, index, oldState);
                }

                if (newHasEntity && !oldHasEntity)
                {
                    OnBlockEntityPlaced?.Invoke(chunkCoord, index, state);
                }

                OnBlockChanged?.Invoke(worldCoord, state);
            }

            dirtiedChunks.Add(chunkCoord);
            DirtyNeighborBorders(chunkCoord, localX, localY, localZ, dirtiedChunks);
        }

        /// <summary>
        ///     Applies deferred edits that arrived while a chunk was in Meshing state.
        ///     Writes the edits to ChunkData and fires block entity events for each edit.
        ///     Called by MeshScheduler.PollCompleted after the mesh job finishes.
        /// </summary>
        internal void ApplyDeferredEdits(ManagedChunk chunk)
        {
            // Complete any in-flight LiquidSimJob holding a [ReadOnly] safety lock
            // on chunk.Data before writing deferred edits.
            chunk.LiquidJobHandle.Complete();

            NativeArray<StateId> chunkData = chunk.Data;
            _deferredDirtiedCache.Clear();
            _deferredDirtiedCache.Add(chunk.Coord);

            for (int di = 0; di < chunk.DeferredEdits.Count; di++)
            {
                DeferredEdit edit = chunk.DeferredEdits[di];
                chunkData[edit.FlatIndex] = edit.NewState;
                chunk.PendingEditIndices.Add(edit.FlatIndex);

                // Unpack flat index to local coordinates
                int localY = edit.FlatIndex / ChunkConstants.SizeSquared;
                int remainder = edit.FlatIndex % ChunkConstants.SizeSquared;
                int localZ = remainder / ChunkConstants.Size;
                int localX = remainder % ChunkConstants.Size;

                // Fire block entity events now that the voxel write has happened
                bool editOldHasEntity = _nativeStateRegistry.States.IsCreated &&
                                        edit.OldState.Value < _nativeStateRegistry.States.Length &&
                                        _nativeStateRegistry.States[edit.OldState.Value].HasBlockEntity;
                bool editNewHasEntity = _nativeStateRegistry.States.IsCreated &&
                                        edit.NewState.Value < _nativeStateRegistry.States.Length &&
                                        _nativeStateRegistry.States[edit.NewState.Value].HasBlockEntity;

                if (editOldHasEntity && !editNewHasEntity)
                {
                    OnBlockEntityRemoved?.Invoke(chunk.Coord, edit.FlatIndex, edit.OldState);
                }

                if (editNewHasEntity && !editOldHasEntity)
                {
                    OnBlockEntityPlaced?.Invoke(chunk.Coord, edit.FlatIndex, edit.NewState);
                }

                // Fire network dirty tracking event
                if (OnBlockChanged is not null)
                {
                    int3 worldCoord = new(
                        chunk.Coord.x * ChunkConstants.Size + localX,
                        chunk.Coord.y * ChunkConstants.Size + localY,
                        chunk.Coord.z * ChunkConstants.Size + localZ);
                    OnBlockChanged.Invoke(worldCoord, edit.NewState);
                }

                // Dirty neighbor chunks for border-touching edits
                DirtyNeighborBorders(chunk.Coord, localX, localY, localZ, _deferredDirtiedCache);
            }

            chunk.DeferredEdits.Clear();
            _setChunkState(chunk, ChunkState.RelightPending);
        }

        /// <summary>
        ///     Dirties neighbor chunks when an edit is on a chunk border face (local coord 0 or 31).
        ///     Extracted to avoid duplication between normal and deferred edit paths.
        /// </summary>
        private void DirtyNeighborBorders(int3 chunkCoord, int localX, int localY, int localZ,
            List<int3> dirtiedChunks)
        {
            if (localX == 0)
            {
                DirtyNeighborChunk(chunkCoord + new int3(-1, 0, 0), dirtiedChunks);
            }

            if (localX == ChunkConstants.SizeMask)
            {
                DirtyNeighborChunk(chunkCoord + new int3(1, 0, 0), dirtiedChunks);
            }

            if (localY == 0)
            {
                DirtyNeighborChunk(chunkCoord + new int3(0, -1, 0), dirtiedChunks);
            }

            if (localY == ChunkConstants.SizeMask)
            {
                DirtyNeighborChunk(chunkCoord + new int3(0, 1, 0), dirtiedChunks);
            }

            if (localZ == 0)
            {
                DirtyNeighborChunk(chunkCoord + new int3(0, 0, -1), dirtiedChunks);
            }

            if (localZ == ChunkConstants.SizeMask)
            {
                DirtyNeighborChunk(chunkCoord + new int3(0, 0, 1), dirtiedChunks);
            }
        }

        /// <summary>Marks a neighbor chunk for remesh if it is Ready, or flags NeedsRemesh if Meshing.</summary>
        private void DirtyNeighborChunk(int3 neighborCoord, List<int3> dirtiedChunks)
        {
            ManagedChunk neighbor = _getChunk(neighborCoord);

            if (neighbor == null || neighbor.State < ChunkState.RelightPending)
            {
                return;
            }

            neighbor.HasPlayerEdit = true;

            if (neighbor.State == ChunkState.Ready)
            {
                _setChunkState(neighbor, ChunkState.Generated);
                dirtiedChunks.Add(neighborCoord);
            }
            else if (neighbor.State == ChunkState.Meshing)
            {
                neighbor.NeedsRemesh = true;
            }
        }
    }
}
