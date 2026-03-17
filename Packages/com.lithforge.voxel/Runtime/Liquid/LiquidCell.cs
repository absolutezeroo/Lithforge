using System.Runtime.CompilerServices;

namespace Lithforge.Voxel.Liquid
{
    /// <summary>
    /// Static utility for 1-byte packed liquid cells.
    /// Bit layout:
    ///   [3:0] Level   — 0 = empty, 1–7 = flowing height (7 = strongest flow near source)
    ///   [4]   Source  — 1 = source block (infinite supply, conceptual level 8)
    ///   [5]   Settled — 1 = no pending flow changes this update cycle
    ///   [7:6] Reserved
    ///
    /// Source cells:  Source=1, Level ignored (effective level = 8)
    /// Flowing cells: Source=0, Level=1–7
    /// Empty cells:   All bits 0 (Source=0, Level=0)
    /// </summary>
    public static class LiquidCell
    {
        public const byte LevelMask = 0x0F;
        public const byte SourceFlag = 0x10;
        public const byte SettledFlag = 0x20;
        public const byte MaxFlowingLevel = 7;
        public const byte SourceLevel = 8;
        public const byte Empty = 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte GetLevel(byte cell)
        {
            return (byte)(cell & LevelMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSource(byte cell)
        {
            return (cell & SourceFlag) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSettled(byte cell)
        {
            return (cell & SettledFlag) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsEmpty(byte cell)
        {
            return (cell & (LevelMask | SourceFlag)) == 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasLiquid(byte cell)
        {
            return (cell & (LevelMask | SourceFlag)) != 0;
        }

        /// <summary>
        /// Returns effective level for flow calculations.
        /// Sources = 8, flowing = 1–7, empty = 0.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte GetEffectiveLevel(byte cell)
        {
            if ((cell & SourceFlag) != 0)
            {
                return SourceLevel;
            }

            return (byte)(cell & LevelMask);
        }

        /// <summary>
        /// Returns the visual level used for meshing (0 = source/tallest, 7 = lowest flowing).
        /// This is the inverse mapping: source → 0, flowing 7 → 1, flowing 1 → 7.
        /// Height formula: (8 - visualLevel) / 9.0
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte GetVisualLevel(byte cell)
        {
            if ((cell & SourceFlag) != 0)
            {
                return 0;
            }

            byte level = (byte)(cell & LevelMask);

            if (level == 0)
            {
                return 0;
            }

            return (byte)(8 - level);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte MakeFlowing(byte level)
        {
            return (byte)(level & LevelMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte MakeSource()
        {
            return SourceFlag;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte SetSettled(byte cell, bool settled)
        {
            if (settled)
            {
                return (byte)(cell | SettledFlag);
            }

            return (byte)(cell & ~SettledFlag);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte ClearSettled(byte cell)
        {
            return (byte)(cell & ~SettledFlag);
        }
    }
}
