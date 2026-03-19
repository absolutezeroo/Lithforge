namespace Lithforge.Network.Transport
{
    /// <summary>
    /// Types of events returned by INetworkTransport.PollEvent.
    /// </summary>
    public enum NetworkEventType : byte
    {
        /// <summary>
        /// No more events available this frame.
        /// </summary>
        Empty = 0,

        /// <summary>
        /// A new connection was established.
        /// </summary>
        Connect = 1,

        /// <summary>
        /// Data was received on an existing connection.
        /// </summary>
        Data = 2,

        /// <summary>
        /// A connection was closed or lost.
        /// </summary>
        Disconnect = 3,
    }
}
