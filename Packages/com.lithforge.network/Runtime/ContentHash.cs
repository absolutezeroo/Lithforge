using System;

namespace Lithforge.Network
{
    public readonly struct ContentHash : IEquatable<ContentHash>
    {
        public static readonly ContentHash Empty = new ContentHash(0, 0);

        public readonly ulong High;
        public readonly ulong Low;

        public ContentHash(ulong high, ulong low)
        {
            High = high;
            Low = low;
        }

        public bool Equals(ContentHash other)
        {
            return High == other.High && Low == other.Low;
        }

        public override bool Equals(object obj)
        {
            return obj is ContentHash other && Equals(other);
        }

        public override int GetHashCode()
        {
            return High.GetHashCode() ^ Low.GetHashCode();
        }

        public override string ToString()
        {
            return High.ToString("x16") + Low.ToString("x16");
        }

        public static bool operator ==(ContentHash left, ContentHash right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ContentHash left, ContentHash right)
        {
            return !left.Equals(right);
        }
    }
}
