using Lithforge.Network.Transport;

namespace Lithforge.Network.Tests.Mocks
{
    /// <summary>Minimal mock of <see cref="INetworkTransport"/> for testing with a real NetworkServer.</summary>
    internal sealed class MockTransport : INetworkTransport
    {
        /// <summary>Number of times Send was called.</summary>
        public int SendCallCount;

        /// <summary>No-op update.</summary>
        public void Update()
        {
        }

        /// <summary>Always succeeds.</summary>
        public bool Listen(ushort port)
        {
            return true;
        }

        /// <summary>Returns a dummy connection ID.</summary>
        public ConnectionId Connect(string address, ushort port)
        {
            return new ConnectionId(0);
        }

        /// <summary>No-op disconnect.</summary>
        public void Disconnect(ConnectionId connectionId)
        {
        }

        /// <summary>Returns Empty (no events).</summary>
        public NetworkEventType PollEvent(
            out ConnectionId connectionId,
            out byte[] data,
            out int offset,
            out int length)
        {
            connectionId = ConnectionId.Invalid;
            data = null;
            offset = 0;
            length = 0;
            return NetworkEventType.Empty;
        }

        /// <summary>Increments counter and succeeds.</summary>
        public bool Send(ConnectionId connectionId, int pipelineId, byte[] data, int offset, int length)
        {
            SendCallCount++;
            return true;
        }

        /// <summary>No-op dispose.</summary>
        public void Dispose()
        {
        }
    }
}
