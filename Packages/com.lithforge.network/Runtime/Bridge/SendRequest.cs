namespace Lithforge.Network.Bridge
{
    /// <summary>
    ///     Carries one outbound send from the server thread to the main thread for
    ///     delivery through the real transport.
    /// </summary>
    internal sealed class SendRequest
    {
        /// <summary>Target connection for this send.</summary>
        public ConnectionId ConnectionId;

        /// <summary>Pipeline to send on (reliable, unreliable, etc.).</summary>
        public int PipelineId;

        /// <summary>Defensively copied payload bytes.</summary>
        public byte[] Data;

        /// <summary>Start offset within <see cref="Data" />.</summary>
        public int Offset;

        /// <summary>Length of valid data starting at <see cref="Offset" />.</summary>
        public int Length;
    }
}
