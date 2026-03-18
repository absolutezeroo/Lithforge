using System;

namespace Lithforge.Network
{
    public readonly struct ConnectionId : IEquatable<ConnectionId>
    {
        public static readonly ConnectionId Invalid = new(-1);

        public readonly int Value;

        public ConnectionId(int value)
        {
            Value = value;
        }

        public bool IsValid
        {
            get { return Value >= 0; }
        }

        public bool Equals(ConnectionId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is ConnectionId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public static bool operator ==(ConnectionId left, ConnectionId right)
        {
            return left.Value == right.Value;
        }

        public static bool operator !=(ConnectionId left, ConnectionId right)
        {
            return left.Value != right.Value;
        }
    }
}
