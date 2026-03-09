using System.Runtime.CompilerServices;

namespace Lithforge.WorldGen.Lighting
{
    public static class LightUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte GetSunLight(byte packed)
        {
            return (byte)(packed >> 4);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte GetBlockLight(byte packed)
        {
            return (byte)(packed & 0x0F);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Pack(byte sun, byte block)
        {
            return (byte)((sun << 4) | (block & 0x0F));
        }
    }
}
