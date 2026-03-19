using System.Collections.Generic;

using Lithforge.Network.Chunk;

using Unity.Mathematics;

namespace Lithforge.Network.Bridge
{
    /// <summary>
    ///     <see cref="IDirtyChangeSource" /> implementation consumed by <see cref="Server.ServerGameLoop" />
    ///     on the server thread. Reads dirty block change snapshots that were enqueued by
    ///     <see cref="MainThreadBridgePump" /> after completing a world tick on the main thread.
    /// </summary>
    internal sealed class BridgedDirtyTracker : IDirtyChangeSource
    {
        /// <summary>Shared cross-thread state.</summary>
        private readonly ServerThreadBridge _bridge;

        /// <summary>Empty dictionary returned when no snapshot is available.</summary>
        private readonly Dictionary<int3, List<BlockChangeEntry>> _empty = new();

        /// <summary>Creates a bridged dirty tracker backed by the given shared bridge.</summary>
        public BridgedDirtyTracker(ServerThreadBridge bridge)
        {
            _bridge = bridge;
        }

        /// <summary>
        ///     Dequeues the latest dirty snapshot from the bridge. Returns an empty dictionary
        ///     if no snapshot has been enqueued (should not happen during normal operation).
        /// </summary>
        public Dictionary<int3, List<BlockChangeEntry>> FlushAll()
        {
            if (_bridge.DirtySnapshots.TryDequeue(
                    out Dictionary<int3, List<BlockChangeEntry>> snapshot))
            {
                return snapshot;
            }

            return _empty;
        }
    }
}
