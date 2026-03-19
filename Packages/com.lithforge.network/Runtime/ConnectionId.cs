using System;

namespace Lithforge.Network
{
    /// <summary>
    /// Lightweight value type identifying a network connection within a transport layer.
    /// Wraps a single integer; negative values are considered invalid.
    /// </summary>
    public readonly struct ConnectionId : IEquatable<ConnectionId>
    {
        /// <summary>
        /// Sentinel value representing no connection.
        /// </summary>
        public static readonly ConnectionId Invalid = new(-1);

        /// <summary>
        /// The underlying integer identifier assigned by the transport.
        /// </summary>
        public readonly int Value;

        /// <summary>
        /// Creates a ConnectionId wrapping the given integer value.
        /// </summary>
        public ConnectionId(int value)
        {
            Value = value;
        }

        /// <summary>
        /// Whether this connection identifier refers to a valid connection (non-negative value).
        /// </summary>
        public bool IsValid
        {
            get { return Value >= 0; }
        }

        /// <summary>
        /// Returns true if both ConnectionId values are equal.
        /// </summary>
        public bool Equals(ConnectionId other)
        {
            return Value == other.Value;
        }

        /// <summary>
        /// Returns true if the object is a ConnectionId with the same value.
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is ConnectionId other && Equals(other);
        }

        /// <summary>
        /// Returns the underlying integer value as the hash code.
        /// </summary>
        public override int GetHashCode()
        {
            return Value;
        }

        /// <summary>
        /// Returns the string representation of the underlying integer value.
        /// </summary>
        public override string ToString()
        {
            return Value.ToString();
        }

        /// <summary>
        /// Equality operator comparing two ConnectionId values.
        /// </summary>
        public static bool operator ==(ConnectionId left, ConnectionId right)
        {
            return left.Value == right.Value;
        }

        /// <summary>
        /// Inequality operator comparing two ConnectionId values.
        /// </summary>
        public static bool operator !=(ConnectionId left, ConnectionId right)
        {
            return left.Value != right.Value;
        }
    }
}
