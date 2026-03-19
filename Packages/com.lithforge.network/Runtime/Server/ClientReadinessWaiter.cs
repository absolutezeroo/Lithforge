using System.Collections.Generic;

using Lithforge.Network;

namespace Lithforge.Network.Server
{
    /// <summary>
    ///     Tracks per-peer Loading-state entry ticks for timeout enforcement.
    ///     When a peer enters Loading, record the start tick. Each server tick, check
    ///     if the elapsed ticks exceed the configured timeout. Used by
    ///     <see cref="ServerGameLoop" /> to force-transition peers that never send
    ///     <see cref="Messages.ClientReadyMessage" />.
    /// </summary>
    public sealed class ClientReadinessWaiter
    {
        private readonly Dictionary<int, uint> _loadStartTick = new();

        private readonly uint _timeoutTicks;

        /// <summary>
        ///     Creates a new waiter with the given timeout in server ticks.
        ///     At 30 TPS, 900 ticks = 30 seconds.
        /// </summary>
        public ClientReadinessWaiter(uint timeoutTicks)
        {
            _timeoutTicks = timeoutTicks;
        }

        /// <summary>Records when a peer entered the Loading state.</summary>
        public void OnPeerEnteredLoading(ConnectionId id, uint currentTick)
        {
            _loadStartTick[id.Value] = currentTick;
        }

        /// <summary>Returns true if the peer has been in Loading state longer than the timeout.</summary>
        public bool IsTimedOut(ConnectionId id, uint currentTick)
        {
            if (!_loadStartTick.TryGetValue(id.Value, out uint startTick))
            {
                return false;
            }

            return currentTick - startTick >= _timeoutTicks;
        }

        /// <summary>Removes tracking for a peer that has transitioned to Playing.</summary>
        public void OnPeerReady(ConnectionId id)
        {
            _loadStartTick.Remove(id.Value);
        }

        /// <summary>Removes tracking for a peer that has disconnected.</summary>
        public void OnPeerRemoved(ConnectionId id)
        {
            _loadStartTick.Remove(id.Value);
        }
    }
}
