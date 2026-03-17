namespace Lithforge.Network.Connection
{
    /// <summary>
    /// Server-side per-peer data tracking connection state, identity, and RTT.
    /// </summary>
    public sealed class PeerInfo
    {
        public ConnectionId ConnectionId { get; }
        public ConnectionStateMachine StateMachine { get; }
        public ushort AssignedPlayerId { get; internal set; }
        public string PlayerName { get; internal set; }
        public float LastPingTime { get; internal set; }
        public float RoundTripTime { get; internal set; }

        public PeerInfo(ConnectionId connectionId)
        {
            ConnectionId = connectionId;
            StateMachine = new ConnectionStateMachine();
            AssignedPlayerId = 0;
            PlayerName = "";
            LastPingTime = 0f;
            RoundTripTime = 0f;
        }
    }
}
