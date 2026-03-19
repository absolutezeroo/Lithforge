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
        /// <summary>Block ordinal index (not the StateId, but the block's sequential registration number).</summary>
        public ushort BlockId;

        /// <summary>Packed boolean flags (opaque, full cube, air, emits light, fluid, has block entity).</summary>
        public byte Flags;

        /// <summary>Render layer index (0=opaque, 1=cutout, 2=translucent).</summary>
        public byte RenderLayer;

        /// <summary>Light emission level (0-15). Non-zero means the block emits light.</summary>
        public byte LightEmission;

        /// <summary>Light filter attenuation value. 0 = fully transparent to light.</summary>
        public byte LightFilter;

        /// <summary>Collision shape type (0=full cube, 1=none).</summary>
        public byte CollisionShape;

        /// <summary>Base texture index, used as fallback for texture atlas lookup.</summary>
        public byte TextureIndexBase;

        /// <summary>Packed RGBA minimap color as uint.</summary>
        public uint MapColor;

        /// <summary>Texture atlas index for the north face (-Z).</summary>
        public ushort TexNorth;

        /// <summary>Texture atlas index for the south face (+Z).</summary>
        public ushort TexSouth;

        /// <summary>Texture atlas index for the east face (+X).</summary>
        public ushort TexEast;

        /// <summary>Texture atlas index for the west face (-X).</summary>
        public ushort TexWest;

        /// <summary>Texture atlas index for the up face (+Y).</summary>
        public ushort TexUp;

        /// <summary>Texture atlas index for the down face (-Y).</summary>
        public ushort TexDown;

        /// <summary>True if this block is visually opaque (blocks rendering of adjacent faces).</summary>
        public bool IsOpaque
        {
            get
            {
                return (Flags & 1) != 0;
            }
        }

        /// <summary>True if this block occupies the full 1x1x1 voxel volume for collision.</summary>
        public bool IsFullCube
        {
            get
            {
                return (Flags & 2) != 0;
            }
        }

        /// <summary>True if this is the air block (StateId 0).</summary>
        public bool IsAir
        {
            get
            {
                return (Flags & 4) != 0;
            }
        }

        /// <summary>True if this block emits light (LightEmission > 0).</summary>
        public bool EmitsLight
        {
            get
            {
                return (Flags & 8) != 0;
            }
        }

        /// <summary>Flag bit for visual opacity.</summary>
        public const byte FlagOpaque = 1;

        /// <summary>Flag bit for full cube collision.</summary>
        public const byte FlagFullCube = 2;

        /// <summary>Flag bit for the air block.</summary>
        public const byte FlagAir = 4;

        /// <summary>Flag bit for light-emitting blocks.</summary>
        public const byte FlagEmitsLight = 8;

        /// <summary>Flag bit for fluid blocks (water, lava). Enables top-face height offset.</summary>
        public const byte FlagFluid = 16;

        /// <summary>Flag bit for blocks with associated block entities.</summary>
        public const byte FlagHasBlockEntity = 32;

        // Bit 6 of Flags is reserved (formerly V1 tint type, now unused).
        // Per-face tint is stored in AtlasEntry.BaseTintPacked / OverlayTintPacked.

        /// <summary>True if this block is a fluid (water, lava).</summary>
        public bool IsFluid
        {
            get
            {
                return (Flags & FlagFluid) != 0;
            }
        }

        /// <summary>True if this block has an associated block entity (inventory, processing, etc.).</summary>
        public bool HasBlockEntity
        {
            get
            {
                return (Flags & FlagHasBlockEntity) != 0;
            }
        }
    }
}
