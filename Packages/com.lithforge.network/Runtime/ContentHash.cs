using System;

namespace Lithforge.Network
{
    /// <summary>
    /// Immutable 128-bit hash used to verify that client and server share identical content definitions.
    /// The high 64 bits cover entry metadata; the low 64 bits cover per-state render/physics data.
    /// </summary>
    public readonly struct ContentHash : IEquatable<ContentHash>
    {
        /// <summary>
        /// A zeroed-out content hash representing no content.
        /// </summary>
        public static readonly ContentHash Empty = new(0, 0);

        /// <summary>
        /// Upper 64 bits of the hash, derived from block entry metadata.
        /// </summary>
        public readonly ulong High;

        /// <summary>
        /// Lower 64 bits of the hash, derived from per-state BlockStateCompact data.
        /// </summary>
        public readonly ulong Low;

        /// <summary>
        /// Creates a ContentHash from the given high and low 64-bit halves.
        /// </summary>
        public ContentHash(ulong high, ulong low)
        {
            High = high;
            Low = low;
        }

        /// <summary>
        /// Returns true if both halves of the hash match.
        /// </summary>
        public bool Equals(ContentHash other)
        {
            return High == other.High && Low == other.Low;
        }

        /// <summary>
        /// Returns true if the object is a ContentHash with the same value.
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is ContentHash other && Equals(other);
        }

        /// <summary>
        /// Returns an XOR of the high and low hash codes.
        /// </summary>
        public override int GetHashCode()
        {
            return High.GetHashCode() ^ Low.GetHashCode();
        }

        /// <summary>
        /// Returns the 32-character lowercase hexadecimal representation of the 128-bit hash.
        /// </summary>
        public override string ToString()
        {
            return High.ToString("x16") + Low.ToString("x16");
        }

        /// <summary>
        /// Equality operator comparing two ContentHash values.
        /// </summary>
        public static bool operator ==(ContentHash left, ContentHash right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Inequality operator comparing two ContentHash values.
        /// </summary>
        public static bool operator !=(ContentHash left, ContentHash right)
        {
            return !left.Equals(right);
        }
    }
}
