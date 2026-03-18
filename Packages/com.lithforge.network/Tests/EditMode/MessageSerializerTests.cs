using Lithforge.Network;
using Lithforge.Network.Message;
using Lithforge.Network.Messages;
using NUnit.Framework;

namespace Lithforge.Network.Tests
{
    [TestFixture]
    public sealed class MessageSerializerTests
    {
        [Test]
        public void MessageHeader_RoundTrip_PreservesValues()
        {
            byte[] buffer = new byte[16];
            MessageHeader.Write(buffer, 0, MessageType.HandshakeRequest, 1234, 0xAB);

            MessageHeader header = MessageHeader.Read(buffer, 0);

            Assert.AreEqual(MessageType.HandshakeRequest, header.Type);
            Assert.AreEqual(1234, header.PayloadLength);
            Assert.AreEqual(0xAB, header.Flags);
        }

        [Test]
        public void HandshakeRequest_RoundTrip()
        {
            HandshakeRequestMessage original = new()
            {
                ProtocolVersion = 1,
                ContentHash = new ContentHash(0xDEADBEEF, 0xCAFEBABE),
                PlayerName = "TestPlayer",
            };

            byte[] buffer = new byte[128];
            int written = original.Serialize(buffer, 0);
            HandshakeRequestMessage deserialized = HandshakeRequestMessage.Deserialize(buffer, 0, written);

            Assert.AreEqual(original.ProtocolVersion, deserialized.ProtocolVersion);
            Assert.AreEqual(original.ContentHash, deserialized.ContentHash);
            Assert.AreEqual(original.PlayerName, deserialized.PlayerName);
        }

        [Test]
        public void HandshakeResponse_RoundTrip()
        {
            HandshakeResponseMessage original = new()
            {
                Accepted = true,
                RejectReason = HandshakeRejectReason.None,
                PlayerId = 42,
                ServerTick = 12345,
                WorldSeed = 9876543210UL,
            };

            byte[] buffer = new byte[128];
            int written = original.Serialize(buffer, 0);
            HandshakeResponseMessage deserialized = HandshakeResponseMessage.Deserialize(buffer, 0, written);

            Assert.AreEqual(original.Accepted, deserialized.Accepted);
            Assert.AreEqual(original.RejectReason, deserialized.RejectReason);
            Assert.AreEqual(original.PlayerId, deserialized.PlayerId);
            Assert.AreEqual(original.ServerTick, deserialized.ServerTick);
            Assert.AreEqual(original.WorldSeed, deserialized.WorldSeed);
        }

        [Test]
        public void HandshakeResponse_Rejected_RoundTrip()
        {
            HandshakeResponseMessage original = new()
            {
                Accepted = false,
                RejectReason = HandshakeRejectReason.ContentMismatch,
                PlayerId = 0,
                ServerTick = 0,
                WorldSeed = 0,
            };

            byte[] buffer = new byte[128];
            int written = original.Serialize(buffer, 0);
            HandshakeResponseMessage deserialized = HandshakeResponseMessage.Deserialize(buffer, 0, written);

            Assert.IsFalse(deserialized.Accepted);
            Assert.AreEqual(HandshakeRejectReason.ContentMismatch, deserialized.RejectReason);
        }

        [Test]
        public void DisconnectMessage_RoundTrip()
        {
            DisconnectMessage original = new()
            {
                Reason = DisconnectReason.Kicked,
            };

            byte[] buffer = new byte[16];
            int written = original.Serialize(buffer, 0);
            DisconnectMessage deserialized = DisconnectMessage.Deserialize(buffer, 0, written);

            Assert.AreEqual(DisconnectReason.Kicked, deserialized.Reason);
        }

        [Test]
        public void PingMessage_RoundTrip()
        {
            PingMessage original = new()
                { Timestamp = 123.456f };

            byte[] buffer = new byte[16];
            int written = original.Serialize(buffer, 0);
            PingMessage deserialized = PingMessage.Deserialize(buffer, 0, written);

            Assert.AreEqual(original.Timestamp, deserialized.Timestamp, 0.0001f);
        }

        [Test]
        public void PongMessage_RoundTrip()
        {
            PongMessage original = new()
            {
                EchoTimestamp = 123.456f,
                ServerTick = 99999,
            };

            byte[] buffer = new byte[16];
            int written = original.Serialize(buffer, 0);
            PongMessage deserialized = PongMessage.Deserialize(buffer, 0, written);

            Assert.AreEqual(original.EchoTimestamp, deserialized.EchoTimestamp, 0.0001f);
            Assert.AreEqual(original.ServerTick, deserialized.ServerTick);
        }

        [Test]
        public void WriteMessage_IncludesHeaderAndPayload()
        {
            PingMessage msg = new()
                { Timestamp = 1.0f };

            int totalBytes = MessageSerializer.WriteMessage(msg, out byte[] buffer);

            Assert.AreEqual(MessageHeader.Size + PingMessage.Size, totalBytes);

            MessageHeader header = MessageHeader.Read(buffer, 0);
            Assert.AreEqual(MessageType.Ping, header.Type);
            Assert.AreEqual(PingMessage.Size, header.PayloadLength);
            Assert.AreEqual(0, header.Flags);
        }

        [Test]
        public void UShort_RoundTrip()
        {
            byte[] buffer = new byte[4];
            MessageSerializer.WriteUShort(buffer, 0, 0xABCD);
            ushort result = MessageSerializer.ReadUShort(buffer, 0);

            Assert.AreEqual(0xABCD, result);
        }

        [Test]
        public void UInt_RoundTrip()
        {
            byte[] buffer = new byte[8];
            MessageSerializer.WriteUInt(buffer, 0, 0xDEADBEEF);
            uint result = MessageSerializer.ReadUInt(buffer, 0);

            Assert.AreEqual(0xDEADBEEF, result);
        }

        [Test]
        public void ULong_RoundTrip()
        {
            byte[] buffer = new byte[16];
            MessageSerializer.WriteULong(buffer, 0, 0xDEADBEEFCAFEBABE);
            ulong result = MessageSerializer.ReadULong(buffer, 0);

            Assert.AreEqual(0xDEADBEEFCAFEBABE, result);
        }

        [Test]
        public void Float_RoundTrip()
        {
            byte[] buffer = new byte[8];
            MessageSerializer.WriteFloat(buffer, 0, 3.14159f);
            float result = MessageSerializer.ReadFloat(buffer, 0);

            Assert.AreEqual(3.14159f, result, 0.00001f);
        }

        [Test]
        public void HandshakeRequest_EmptyName_RoundTrip()
        {
            HandshakeRequestMessage original = new()
            {
                ProtocolVersion = 1,
                ContentHash = new ContentHash(0, 0),
                PlayerName = "",
            };

            byte[] buffer = new byte[128];
            int written = original.Serialize(buffer, 0);
            HandshakeRequestMessage deserialized = HandshakeRequestMessage.Deserialize(buffer, 0, written);

            Assert.AreEqual("", deserialized.PlayerName);
        }

        [Test]
        public void GetSerializedSize_MatchesActualOutput()
        {
            HandshakeRequestMessage request = new()
            {
                ProtocolVersion = 1,
                ContentHash = new ContentHash(1, 2),
                PlayerName = "Hello",
            };

            byte[] buffer = new byte[128];
            int predicted = request.GetSerializedSize();
            int actual = request.Serialize(buffer, 0);

            Assert.AreEqual(predicted, actual, "GetSerializedSize must match actual Serialize output");
        }
    }
}
