using System.Runtime.InteropServices;

namespace Lithforge.Voxel.Block
{
    /// <summary>
    /// Pre-resolved, cached block state data for Burst jobs.
    /// All rendering/physics-relevant flags are pre-computed at content load.
    /// Blittable struct — no managed references.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct BlockStateCompact
    {
        public ushort BlockId;
        public byte Flags;
        public byte RenderLayer;
        public byte LightEmission;
        public byte LightFilter;
        public byte CollisionShape;
        public byte TextureIndexBase;
        public uint MapColor;

        public ushort TexNorth;
        public ushort TexSouth;
        public ushort TexEast;
        public ushort TexWest;
        public ushort TexUp;
        public ushort TexDown;

        public bool IsOpaque
        {
            get { return (Flags & 1) != 0; }
        }

        public bool IsFullCube
        {
            get { return (Flags & 2) != 0; }
        }

        public bool IsAir
        {
            get { return (Flags & 4) != 0; }
        }

        public bool EmitsLight
        {
            get { return (Flags & 8) != 0; }
        }

        public const byte FlagOpaque = 1;

        public const byte FlagFullCube = 2;

        public const byte FlagAir = 4;

        public const byte FlagEmitsLight = 8;

        public const byte FlagFluid = 16;

        /// <summary>Bits 5-6 of Flags encode the tint type (0=none, 1=grass, 2=foliage, 3=water).</summary>
        public const byte FlagTintShift = 5;

        /// <summary>Mask for bits 5-6: 0x60 = 0b0110_0000.</summary>
        public const byte FlagTintMask = 0x60;

        public bool IsFluid
        {
            get { return (Flags & FlagFluid) != 0; }
        }

        public byte TintType
        {
            get { return (byte)((Flags & FlagTintMask) >> FlagTintShift); }
        }
    }
}
