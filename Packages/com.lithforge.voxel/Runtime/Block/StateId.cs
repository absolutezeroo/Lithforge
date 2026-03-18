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
        public readonly ushort Value;

        public static readonly StateId Air = new(0);

        public StateId(ushort value)
        {
            Value = value;
        }

        public bool Equals(StateId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is StateId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return $"StateId({Value})";
        }

        public static bool operator ==(StateId left, StateId right)
        {
            return left.Value == right.Value;
        }

        public static bool operator !=(StateId left, StateId right)
        {
            return left.Value != right.Value;
        }
    }
}
