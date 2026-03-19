using Lithforge.Network.Server;

namespace Lithforge.Network.Connection
{
    /// <summary>
    ///     Server-side per-peer data tracking connection state, identity, and RTT.
    /// </summary>
    public sealed class PeerInfo
    {
        public PeerInfo(ConnectionId connectionId)
        {
            ConnectionId = connectionId;
            StateMachine = new ConnectionStateMachine();
            AssignedPlayerId = 0;
            PlayerName = "";
            LastPingTime = 0f;
            RoundTripTime = 0f;
            InterestState = null;
        }

        public ConnectionId ConnectionId { get; }

        public ConnectionStateMachine StateMachine { get; }

        public ushort AssignedPlayerId { get; internal set; }

        public string PlayerName { get; internal set; }

        public float LastPingTime { get; internal set; }

        public float RoundTripTime { get; internal set; }

        /// <summary>Time of last received message from this peer. Used for idle timeout.</summary>
        public float LastMessageTime { get; internal set; }

        /// <summary>
        ///     True for the local peer in SP/Host mode (DirectTransport).
        ///     Local peers use the same ClientReadinessTracker as remote clients.
        /// </summary>
        public bool IsLocal { get; set; }

        /// <summary>
        ///     Per-player interest state for chunk streaming and network filtering.
        ///     Allocated when the peer transitions to Loading state, null before that.
        /// </summary>
        public PlayerInterestState InterestState { get; internal set; }

        /// <summary>
        ///     Session token issued to this peer on acceptance. Enables reconnection
        ///     without full re-handshake if the client disconnects and reconnects
        ///     within <see cref="SessionTokenRegistry.TokenLifetimeSeconds" />.
        /// </summary>
        public SessionToken SessionToken { get; internal set; }
    }
}
