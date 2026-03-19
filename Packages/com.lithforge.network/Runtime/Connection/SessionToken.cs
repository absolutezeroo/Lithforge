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

        /// <summary>Initializes a session token with the given raw value.</summary>
        public SessionToken(ulong value)
        {
            Value = value;
        }

        /// <summary>Returns true if this token is not the zero/invalid sentinel.</summary>
        public bool IsValid
        {
            get { return Value != 0; }
        }

        /// <summary>Returns true if this token's value equals the other token's value.</summary>
        public bool Equals(SessionToken other)
        {
            return Value == other.Value;
        }

        /// <summary>Returns true if the object is a SessionToken with the same value.</summary>
        public override bool Equals(object obj)
        {
            return obj is SessionToken other && Equals(other);
        }

        /// <summary>Returns the hash code of the underlying token value.</summary>
        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        /// <summary>Returns a hex-formatted string representation of the token value.</summary>
        public override string ToString()
        {
            return $"SessionToken({Value:X16})";
        }

        /// <summary>Returns true if the left and right tokens have the same value.</summary>
        public static bool operator ==(SessionToken left, SessionToken right)
        {
            return left.Value == right.Value;
        }

        /// <summary>Returns true if the left and right tokens have different values.</summary>
        public static bool operator !=(SessionToken left, SessionToken right)
        {
            return left.Value != right.Value;
        }
    }
}
