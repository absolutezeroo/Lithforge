using System;

namespace Lithforge.Network.Transport
{
    /// <summary>
    ///     Abstraction over the network transport layer.
    ///     Production code uses NetworkDriverWrapper (UTP). Tests can provide a mock.
    ///     All byte[] parameters use offset+length to avoid per-send allocation.
    /// </summary>
    public interface INetworkTransport : IDisposable
    {
        /// <summary>
        ///     Pumps the underlying driver. Must be called once per frame before PollEvent.
        /// </summary>
        public void Update();

        /// <summary>
        ///     Starts listening for incoming connections on the given port.
        /// </summary>
        public bool Listen(ushort port);

        /// <summary>
        ///     Initiates an outgoing connection to the given address and port.
        ///     Returns the ConnectionId assigned to this connection attempt.
        /// </summary>
        public ConnectionId Connect(string address, ushort port);

        /// <summary>
        ///     Gracefully disconnects a connection.
        /// </summary>
        public void Disconnect(ConnectionId connectionId);

        /// <summary>
        ///     Polls the next network event. Returns Empty when no more events are available.
        ///     When eventType is Data, the data/offset/length out parameters are populated.
        ///     The returned byte[] is only valid until the next PollEvent call.
        /// </summary>
        public NetworkEventType PollEvent(
            out ConnectionId connectionId,
            out byte[] data,
            out int offset,
            out int length);

        /// <summary>
        ///     Sends data on the specified pipeline.
        ///     Returns true on success, false if the send queue is full (error -5).
        /// </summary>
        public bool Send(ConnectionId connectionId, int pipelineId, byte[] data, int offset, int length);
    }
}
