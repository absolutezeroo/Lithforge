using System;

using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    ///     Client→Server challenge response. Contains the ECDSA-SHA256 signature of the
    ///     server's challenge nonce, proving the client owns the private key corresponding
    ///     to the public key sent in the handshake request.
    ///     Wire format: [SignatureLength:1][Signature:N]
    /// </summary>
    public struct ChallengeResponseMessage : INetworkMessage
    {
        /// <summary>ECDSA-SHA256 signature of the challenge nonce.</summary>
        public byte[] Signature;

        /// <summary>Returns the MessageType for this message.</summary>
        public MessageType Type
        {
            get { return MessageType.ChallengeResponse; }
        }

        /// <summary>Returns the payload size including the length prefix and signature bytes.</summary>
        public int GetSerializedSize()
        {
            int sigLen = Signature?.Length ?? 0;
            return 1 + sigLen;
        }

        /// <summary>Writes the message payload into the buffer at the given offset.</summary>
        public int Serialize(byte[] buffer, int offset)
        {
            int sigLen = Signature?.Length ?? 0;
            buffer[offset] = (byte)sigLen;
            int written = 1;

            if (sigLen > 0)
            {
                Array.Copy(Signature, 0, buffer, offset + 1, sigLen);
                written += sigLen;
            }

            return written;
        }

        /// <summary>Reads the message from the buffer.</summary>
        public static ChallengeResponseMessage Deserialize(byte[] buffer, int offset, int length)
        {
            ChallengeResponseMessage msg = new();

            if (length < 1)
            {
                return msg;
            }

            int sigLen = buffer[offset];

            if (sigLen > 0 && offset + 1 + sigLen <= offset + length)
            {
                msg.Signature = new byte[sigLen];
                Array.Copy(buffer, offset + 1, msg.Signature, 0, sigLen);
            }

            return msg;
        }
    }
}
