using System.Collections.Generic;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Unity.Mathematics;

namespace Lithforge.Voxel.Network
{
    /// <summary>
    /// Tracks per-chunk block changes for network delta batching.
    /// Subscribe to <see cref="ChunkManager.OnBlockChanged"/> to accumulate changes.
    /// The network transport layer calls <see cref="FlushChanges"/> to retrieve and clear
    /// the accumulated batch for a chunk.
    /// </summary>
    public sealed class ChunkDirtyTracker
    {
        private readonly Dictionary<int3, List<BlockChangeEntry>> _pendingChanges = new();

        /// <summary>
        /// Records a block change. Intended to be wired to <see cref="ChunkManager.OnBlockChanged"/>.
        /// </summary>
        public void OnBlockChanged(int3 worldCoord, StateId newState)
        {
            int3 chunkCoord = ChunkManager.WorldToChunk(worldCoord);

            if (!_pendingChanges.TryGetValue(chunkCoord, out List<BlockChangeEntry> list))
            {
                list = new List<BlockChangeEntry>();
                _pendingChanges[chunkCoord] = list;
            }

            list.Add(new BlockChangeEntry
            {
                Position = worldCoord,
                NewState = newState,
            });
        }

        /// <summary>
        /// Returns and clears the accumulated block changes for a specific chunk.
        /// Returns null if no changes are pending for that chunk.
        /// </summary>
        public List<BlockChangeEntry> FlushChanges(int3 chunkCoord)
        {
            if (!_pendingChanges.TryGetValue(chunkCoord, out List<BlockChangeEntry> list))
            {
                return null;
            }

            _pendingChanges.Remove(chunkCoord);
            return list;
        }

        /// <summary>
        /// Returns true if there are pending block changes for the given chunk.
        /// </summary>
        public bool HasChanges(int3 chunkCoord)
        {
            return _pendingChanges.ContainsKey(chunkCoord) && _pendingChanges[chunkCoord].Count > 0;
        }

        /// <summary>
        /// Returns a snapshot of all chunk coordinates that have pending changes.
        /// Safe to iterate while calling <see cref="FlushChanges"/> per chunk.
        /// </summary>
        public List<int3> GetDirtyChunks()
        {
            return new List<int3>(_pendingChanges.Keys);
        }

        /// <summary>
        /// Flushes all pending changes across all chunks and returns them grouped by chunk coordinate.
        /// Clears the internal state.
        /// </summary>
        public Dictionary<int3, List<BlockChangeEntry>> FlushAll()
        {
            Dictionary<int3, List<BlockChangeEntry>> snapshot = new(_pendingChanges);
            _pendingChanges.Clear();
            return snapshot;
        }

        /// <summary>
        /// Clears all pending changes without returning them.
        /// </summary>
        public void Clear()
        {
            _pendingChanges.Clear();
        }
    }
}
