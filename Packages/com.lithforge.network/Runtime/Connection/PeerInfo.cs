using System;

using Lithforge.Network.Server;
using Lithforge.Voxel.Storage;

namespace Lithforge.Network.Connection
{
    /// <summary>
    ///     Server-side per-peer data tracking connection state, identity, and RTT.
    /// </summary>
    public sealed class PeerInfo
    {
        /// <summary>Creates a new PeerInfo for the given connection with default initial values.</summary>
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

        /// <summary>The transport-level connection ID for this peer.</summary>
        public ConnectionId ConnectionId { get; }

        /// <summary>The connection state machine tracking this peer's lifecycle.</summary>
        public ConnectionStateMachine StateMachine { get; }

        /// <summary>The player ID assigned during handshake acceptance. Zero if not yet assigned.</summary>
        public ushort AssignedPlayerId { get; internal set; }

        /// <summary>The player's display name received during the handshake request.</summary>
        public string PlayerName { get; internal set; }

        /// <summary>Wall-clock time of the last ping sent to this peer.</summary>
        public float LastPingTime { get; internal set; }

        /// <summary>The most recently measured round-trip time for this peer in seconds.</summary>
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

        /// <summary>The player's unique identifier string (Ed25519-derived UUID or "local").</summary>
        public string PlayerUuid { get; internal set; } = "";

        /// <summary>True if this peer has operator privileges on the server.</summary>
        public bool IsAdmin { get; internal set; }

        /// <summary>Loaded player save data, or null if no save exists.</summary>
        public WorldPlayerState PlayerData { get; internal set; }

        /// <summary>The client's public key (SubjectPublicKeyInfo DER) sent during handshake.</summary>
        public byte[] PublicKey { get; internal set; } = Array.Empty<byte>();

        /// <summary>Temporary challenge nonce stored during authentication. Cleared after verification.</summary>
        public byte[] ChallengeNonce { get; internal set; }
    }
}
