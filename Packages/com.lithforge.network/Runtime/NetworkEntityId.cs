using System;

namespace Lithforge.Network
{
    /// <summary>
    /// Typed wrapper for a network entity identifier (player, future mob, etc.).
    /// Wraps a ushort matching the wire format used by player state and input messages.
    /// </summary>
    public readonly struct NetworkEntityId : IEquatable<NetworkEntityId>
    {
        /// <summary>Sentinel value representing no entity.</summary>
        public static readonly NetworkEntityId None = new(0);

        /// <summary>The underlying ushort identifier assigned by the server.</summary>
        public readonly ushort Value;

        /// <summary>Creates a NetworkEntityId wrapping the given ushort value.</summary>
        public NetworkEntityId(ushort value)
        {
            Value = value;
        }

        /// <summary>Returns true if both NetworkEntityId values are equal.</summary>
        public bool Equals(NetworkEntityId other)
        {
            return Value == other.Value;
        }

        /// <summary>Returns true if the object is a NetworkEntityId with the same value.</summary>
        public override bool Equals(object obj)
        {
            return obj is NetworkEntityId other && Equals(other);
        }

        /// <summary>Returns the underlying ushort value as the hash code.</summary>
        public override int GetHashCode()
        {
            return Value;
        }

        /// <summary>Returns the string representation of the underlying ushort value.</summary>
        public override string ToString()
        {
            return Value.ToString();
        }

        /// <summary>Equality operator comparing two NetworkEntityId values.</summary>
        public static bool operator ==(NetworkEntityId left, NetworkEntityId right)
        {
            return left.Value == right.Value;
        }

        /// <summary>Inequality operator comparing two NetworkEntityId values.</summary>
        public static bool operator !=(NetworkEntityId left, NetworkEntityId right)
        {
            return left.Value != right.Value;
        }

        /// <summary>Implicit conversion to ushort for backward compatibility with existing APIs.</summary>
        public static implicit operator ushort(NetworkEntityId id)
        {
            return id.Value;
        }

        /// <summary>Implicit conversion from ushort to NetworkEntityId for convenience.</summary>
        public static implicit operator NetworkEntityId(ushort value)
        {
            return new NetworkEntityId(value);
        }
    }
}
