namespace Lithforge.Network.Transport
{
    /// <summary>
    ///     Factory that creates a paired DirectTransportServer + DirectTransportClient
    ///     sharing crossed in-memory channels. Data sent by one side is received by the other.
    /// </summary>
    public static class DirectTransportPair
    {
        /// <summary>
        /// Creates a matched server/client pair sharing crossed in-memory channels.
        /// </summary>
        public static void Create(out DirectTransportServer server, out DirectTransportClient client)
        {
            // Channel A: client→server (client writes, server reads)
            DirectChannel clientToServer = new();
            // Channel B: server→client (server writes, client reads)
            DirectChannel serverToClient = new();

            server = new DirectTransportServer(
                inbound: clientToServer,
                outbound: serverToClient);

            client = new DirectTransportClient(
                inbound: serverToClient,
                outbound: clientToServer);
        }
    }
}
