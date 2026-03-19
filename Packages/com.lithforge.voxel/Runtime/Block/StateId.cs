using System;
using System.Runtime.InteropServices;

namespace Lithforge.Voxel.Block
{
    /// <summary>
    ///     Index into the global StateRegistry. Blittable, Burst-compatible.
    ///     Value 0 is always AIR — this invariant is hardcoded and cannot be overridden.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct StateId : IEquatable<StateId>
    {
        /// <summary>Raw numeric index into the global state table.</summary>
        public readonly ushort Value;

        /// <summary>Sentinel value for air (always index 0).</summary>
        public static readonly StateId Air = new(0);

        /// <summary>Creates a StateId from a raw index value.</summary>
        public StateId(ushort value)
        {
            Value = value;
        }

        /// <summary>Returns true if both StateIds have the same numeric value.</summary>
        public bool Equals(StateId other)
        {
            return Value == other.Value;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is StateId other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return Value;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"StateId({Value})";
        }

        /// <summary>Equality operator comparing underlying values.</summary>
        public static bool operator ==(StateId left, StateId right)
        {
            return left.Value == right.Value;
        }

        /// <summary>Inequality operator comparing underlying values.</summary>
        public static bool operator !=(StateId left, StateId right)
        {
            return left.Value != right.Value;
        }
    }
}
