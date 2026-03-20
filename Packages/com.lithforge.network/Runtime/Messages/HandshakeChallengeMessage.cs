using System;

using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    ///     Server→Client challenge message. Sent after validating protocol/content in the
    ///     handshake request. Contains a random nonce that the client must sign with its
    ///     private key to prove identity ownership.
    ///     Wire format: [NonceLength:1][Nonce:N]
    /// </summary>
    public struct HandshakeChallengeMessage : INetworkMessage
    {
        /// <summary>Size of the challenge nonce in bytes.</summary>
        public const int NonceSize = 32;

        /// <summary>Random nonce bytes the client must sign.</summary>
        public byte[] Nonce;

        /// <summary>Returns the MessageType for this message.</summary>
        public MessageType Type
        {
            get { return MessageType.HandshakeChallenge; }
        }

        /// <summary>Returns the payload size including the length prefix and nonce bytes.</summary>
        public int GetSerializedSize()
        {
            int nonceLen = Nonce?.Length ?? 0;
            return 1 + nonceLen;
        }

        /// <summary>Writes the message payload into the buffer at the given offset.</summary>
        public int Serialize(byte[] buffer, int offset)
        {
            int nonceLen = Nonce?.Length ?? 0;
            buffer[offset] = (byte)nonceLen;
            int written = 1;

            if (nonceLen > 0)
            {
                Array.Copy(Nonce, 0, buffer, offset + 1, nonceLen);
                written += nonceLen;
            }

            return written;
        }

        /// <summary>Reads the message from the buffer.</summary>
        public static HandshakeChallengeMessage Deserialize(byte[] buffer, int offset, int length)
        {
            HandshakeChallengeMessage msg = new();

            if (length < 1)
            {
                return msg;
            }

            int nonceLen = buffer[offset];

            if (nonceLen > 0 && offset + 1 + nonceLen <= offset + length)
            {
                msg.Nonce = new byte[nonceLen];
                Array.Copy(buffer, offset + 1, msg.Nonce, 0, nonceLen);
            }

            return msg;
        }
    }
}
