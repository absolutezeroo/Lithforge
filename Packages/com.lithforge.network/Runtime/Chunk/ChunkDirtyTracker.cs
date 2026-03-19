using System.Collections.Generic;

using Lithforge.Network.Bridge;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;

using Unity.Mathematics;

namespace Lithforge.Network.Chunk
{
    /// <summary>
    ///     Tracks per-chunk block changes for network delta batching.
    ///     Subscribe to <see cref="ChunkManager.OnBlockChanged" /> to accumulate changes.
    ///     Uses double-buffering to avoid per-tick dictionary allocation: <see cref="FlushAll" />
    ///     swaps the active buffer and returns the previous tick's data without heap allocation.
    /// </summary>
    public sealed class ChunkDirtyTracker : IDirtyChangeSource
    {
        /// <summary>Accumulates changes during the current tick.</summary>
        private Dictionary<int3, List<BlockChangeEntry>> _current = new();

        /// <summary>Holds the flushed data from the previous tick for the caller to read.</summary>
        private Dictionary<int3, List<BlockChangeEntry>> _ready = new();

        /// <summary>
        ///     Records a block change. Intended to be wired to <see cref="ChunkManager.OnBlockChanged" />.
        /// </summary>
        public void OnBlockChanged(int3 worldCoord, StateId newState)
        {
            int3 chunkCoord = ChunkManager.WorldToChunk(worldCoord);

            if (!_current.TryGetValue(chunkCoord, out List<BlockChangeEntry> list))
            {
                list = new List<BlockChangeEntry>();
                _current[chunkCoord] = list;
            }

            list.Add(new BlockChangeEntry
            {
                Position = worldCoord, NewState = newState,
            });
        }

        /// <summary>
        ///     Returns and clears the accumulated block changes for a specific chunk
        ///     from the current buffer. Returns null if no changes are pending for that chunk.
        /// </summary>
        public List<BlockChangeEntry> FlushChanges(int3 chunkCoord)
        {
            if (!_current.TryGetValue(chunkCoord, out List<BlockChangeEntry> list))
            {
                return null;
            }

            _current.Remove(chunkCoord);
            return list;
        }

        /// <summary>
        ///     Returns true if there are pending block changes for the given chunk.
        /// </summary>
        public bool HasChanges(int3 chunkCoord)
        {
            return _current.TryGetValue(chunkCoord, out List<BlockChangeEntry> list) && list.Count > 0;
        }

        /// <summary>
        ///     Returns a snapshot of all chunk coordinates that have pending changes.
        ///     Allocates a new list — intended for diagnostics and tests, not the hot path.
        /// </summary>
        public List<int3> GetDirtyChunks()
        {
            return new List<int3>(_current.Keys);
        }

        /// <summary>
        ///     Swaps the internal double buffers and returns the accumulated changes from this tick.
        ///     The returned dictionary is valid for the duration of the current tick only —
        ///     callers must not hold the reference across ticks.
        ///     Zero heap allocation after warmup (inner lists are cleared and reused).
        /// </summary>
        public Dictionary<int3, List<BlockChangeEntry>> FlushAll()
        {
            // Swap: the old ready becomes the new current (to be cleared and reused)
            Dictionary<int3, List<BlockChangeEntry>> temp = _ready;
            _ready = _current;
            _current = temp;

            // Clear the new current (old ready) for reuse — inner lists keep their backing arrays
            foreach (KeyValuePair<int3, List<BlockChangeEntry>> pair in _current)
            {
                pair.Value.Clear();
            }

            _current.Clear();

            return _ready;
        }

        /// <summary>
        ///     Clears all pending changes without returning them.
        /// </summary>
        public void Clear()
        {
            foreach (KeyValuePair<int3, List<BlockChangeEntry>> pair in _current)
            {
                pair.Value.Clear();
            }

            _current.Clear();

            foreach (KeyValuePair<int3, List<BlockChangeEntry>> pair in _ready)
            {
                pair.Value.Clear();
            }

            _ready.Clear();
        }
    }
}
