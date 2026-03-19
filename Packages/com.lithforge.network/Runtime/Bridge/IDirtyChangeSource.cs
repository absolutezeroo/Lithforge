using System.Collections.Generic;

using Lithforge.Network.Chunk;

using Unity.Mathematics;

namespace Lithforge.Network.Bridge
{
    /// <summary>
    ///     Abstracts the source of per-tick dirty block changes for the server game loop.
    ///     On the main thread, <see cref="Chunk.ChunkDirtyTracker" /> implements this directly.
    ///     On the server thread, <see cref="BridgedDirtyTracker" /> reads from a cross-thread queue.
    /// </summary>
    public interface IDirtyChangeSource
    {
        /// <summary>
        ///     Returns the accumulated block changes since the last call. The returned dictionary
        ///     is valid only for the duration of the current tick — callers must not hold
        ///     the reference across ticks.
        /// </summary>
        public Dictionary<int3, List<BlockChangeEntry>> FlushAll();
    }
}
