using Lithforge.Network.Transport;

namespace Lithforge.Network.Bridge
{
    /// <summary>
    ///     Carries one network event from the main thread (real transport) to the server thread
    ///     (BridgedTransport). Data is defensively copied so the original buffer can be reused.
    /// </summary>
    internal sealed class NetworkEventEnvelope
    {
        /// <summary>The type of event (Connect, Data, Disconnect).</summary>
        public NetworkEventType EventType;

        /// <summary>The connection that produced this event.</summary>
        public ConnectionId ConnectionId;

        /// <summary>Defensively copied payload bytes (null for non-Data events).</summary>
        public byte[] Data;

        /// <summary>Length of valid data in <see cref="Data" />.</summary>
        public int Length;
    }
}
