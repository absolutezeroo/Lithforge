using System;

namespace Lithforge.Network.Connection
{
    /// <summary>
    ///     Opaque reconnection token issued by the server when a player is accepted.
    ///     If the client disconnects and reconnects within the token's lifetime,
    ///     the server can restore the player's state instead of requiring a full
    ///     handshake and chunk re-stream. Zero value means "no token".
    /// </summary>
    public readonly struct SessionToken : IEquatable<SessionToken>
    {
        /// <summary>The token's unique value. Zero = invalid / no token.</summary>
        public readonly ulong Value;

        public SessionToken(ulong value)
        {
            Value = value;
        }

        /// <summary>Returns true if this token is not the zero/invalid sentinel.</summary>
        public bool IsValid
        {
            get { return Value != 0; }
        }

        public bool Equals(SessionToken other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is SessionToken other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return $"SessionToken({Value:X16})";
        }

        public static bool operator ==(SessionToken left, SessionToken right)
        {
            return left.Value == right.Value;
        }

        public static bool operator !=(SessionToken left, SessionToken right)
        {
            return left.Value != right.Value;
        }
    }
}
