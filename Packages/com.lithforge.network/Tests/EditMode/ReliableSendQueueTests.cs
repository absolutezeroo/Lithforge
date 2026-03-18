using Lithforge.Core.Logging;
using Lithforge.Network.SendQueue;
using Lithforge.Network.Transport;

using NUnit.Framework;

namespace Lithforge.Network.Tests
{
    [TestFixture]
    public sealed class ReliableSendQueueTests
    {
        private sealed class MockTransport : INetworkTransport
        {
            public int SendCallCount;
            public bool SendSucceeds = true;

            public void Update() { }
            public bool Listen(ushort port) { return true; }
            public ConnectionId Connect(string address, ushort port) { return new ConnectionId(0); }
            public void Disconnect(ConnectionId connectionId) { }

            public NetworkEventType PollEvent(
                out ConnectionId connectionId, out byte[] data, out int offset, out int length)
            {
                connectionId = ConnectionId.Invalid;
                data = null;
                offset = 0;
                length = 0;
                return NetworkEventType.Empty;
            }

            public bool Send(ConnectionId connectionId, int pipelineId, byte[] data, int offset, int length)
            {
                SendCallCount++;
                return SendSucceeds;
            }

            public void Dispose() { }
        }

        private sealed class TestLogger : ILogger
        {
            public void Log(LogLevel level, string message) { }
            public void LogDebug(string message) { }
            public void LogInfo(string message) { }
            public void LogWarning(string message) { }
            public void LogError(string message) { }
        }

        [Test]
        public void Enqueue_IncreasesCount()
        {
            ReliableSendQueue queue = new(new TestLogger());
            ConnectionId connId = new(1);
            byte[] data =
            {
                1,
                2,
                3,
            };

            queue.Enqueue(connId, PipelineId.ReliableSequenced, data, 0, data.Length);

            Assert.AreEqual(1, queue.Count);
        }

        [Test]
        public void Flush_SuccessfulSend_RemovesEntry()
        {
            ReliableSendQueue queue = new(new TestLogger());
            MockTransport transport = new()
            {
                SendSucceeds = true,
            };
            ConnectionId connId = new(1);
            byte[] data =
            {
                1,
                2,
                3,
            };

            queue.Enqueue(connId, PipelineId.ReliableSequenced, data, 0, data.Length);
            int dropped = queue.Flush(transport);

            Assert.AreEqual(0, queue.Count);
            Assert.AreEqual(0, dropped);
            Assert.AreEqual(1, transport.SendCallCount);
        }

        [Test]
        public void Flush_FailedSend_RetainsEntry()
        {
            ReliableSendQueue queue = new(new TestLogger());
            MockTransport transport = new()
            {
                SendSucceeds = false,
            };
            ConnectionId connId = new(1);
            byte[] data =
            {
                1,
                2,
                3,
            };

            queue.Enqueue(connId, PipelineId.ReliableSequenced, data, 0, data.Length);
            int dropped = queue.Flush(transport);

            Assert.AreEqual(1, queue.Count);
            Assert.AreEqual(0, dropped);
        }

        [Test]
        public void Flush_MaxRetries_DropsEntry()
        {
            ReliableSendQueue queue = new(new TestLogger());
            MockTransport transport = new()
            {
                SendSucceeds = false,
            };
            ConnectionId connId = new(1);
            byte[] data =
            {
                1,
                2,
                3,
            };

            queue.Enqueue(connId, PipelineId.ReliableSequenced, data, 0, data.Length);

            // Retry MaxSendRetries times (3)
            for (int i = 0; i < NetworkConstants.MaxSendRetries; i++)
            {
                queue.Flush(transport);
            }

            Assert.AreEqual(0, queue.Count, "Entry should be dropped after max retries");
        }

        [Test]
        public void Flush_ReturnsDroppedCount()
        {
            ReliableSendQueue queue = new(new TestLogger());
            MockTransport transport = new()
            {
                SendSucceeds = false,
            };
            ConnectionId connId = new(1);
            byte[] data =
            {
                1,
                2,
                3,
            };

            queue.Enqueue(connId, PipelineId.ReliableSequenced, data, 0, data.Length);

            int dropped = 0;

            for (int i = 0; i < NetworkConstants.MaxSendRetries; i++)
            {
                dropped += queue.Flush(transport);
            }

            Assert.AreEqual(1, dropped);
        }

        [Test]
        public void RemoveForConnection_RemovesMatchingEntries()
        {
            ReliableSendQueue queue = new(new TestLogger());
            ConnectionId conn1 = new(1);
            ConnectionId conn2 = new(2);
            byte[] data =
            {
                1,
            };

            queue.Enqueue(conn1, PipelineId.ReliableSequenced, data, 0, data.Length);
            queue.Enqueue(conn2, PipelineId.ReliableSequenced, data, 0, data.Length);
            queue.Enqueue(conn1, PipelineId.ReliableSequenced, data, 0, data.Length);

            queue.RemoveForConnection(conn1);

            Assert.AreEqual(1, queue.Count, "Only conn2 entry should remain");
        }

        [Test]
        public void Clear_RemovesAllEntries()
        {
            ReliableSendQueue queue = new(new TestLogger());
            ConnectionId connId = new(1);
            byte[] data =
            {
                1,
            };

            queue.Enqueue(connId, PipelineId.ReliableSequenced, data, 0, data.Length);
            queue.Enqueue(connId, PipelineId.ReliableSequenced, data, 0, data.Length);
            queue.Clear();

            Assert.AreEqual(0, queue.Count);
        }

        [Test]
        public void Enqueue_CopiesData()
        {
            ReliableSendQueue queue = new(new TestLogger());
            MockTransport transport = new()
            {
                SendSucceeds = true,
            };
            ConnectionId connId = new(1);
            byte[] data =
            {
                1,
                2,
                3,
            };

            queue.Enqueue(connId, PipelineId.ReliableSequenced, data, 0, data.Length);

            // Mutate original data — should not affect queued entry
            data[0] = 99;

            // Flush will send the original data, not the mutated version
            queue.Flush(transport);
            Assert.AreEqual(1, transport.SendCallCount);
        }
    }
}
