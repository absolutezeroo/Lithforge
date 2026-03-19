using System.Runtime.CompilerServices;

namespace Lithforge.WorldGen.Lighting
{
    /// <summary>Nibble-pack/unpack helpers for the dual sunlight + block light byte encoding.</summary>
    public static class LightUtils
    {
        /// <summary>Extracts the sunlight level (0-15) from the high nibble of a packed light byte.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte GetSunLight(byte packed)
        {
            return (byte)(packed >> 4);
        }

        /// <summary>Extracts the block light level (0-15) from the low nibble of a packed light byte.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte GetBlockLight(byte packed)
        {
            return (byte)(packed & 0x0F);
        }

        /// <summary>Packs sunlight (high nibble) and block light (low nibble) into a single byte.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Pack(byte sun, byte block)
        {
            return (byte)((sun << 4) | (block & 0x0F));
        }
    }
}
