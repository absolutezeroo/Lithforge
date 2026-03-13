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

        // Bits 5-6 of Flags are reserved (formerly V1 tint type, now unused).
        // Per-face tint is stored in AtlasEntry.BaseTintPacked / OverlayTintPacked.

        public bool IsFluid
        {
            get { return (Flags & FlagFluid) != 0; }
        }
    }
}
