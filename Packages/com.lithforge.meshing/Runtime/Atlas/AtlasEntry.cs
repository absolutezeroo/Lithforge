using System.Runtime.InteropServices;

namespace Lithforge.Meshing.Atlas
{
    /// <summary>
    /// Blittable per-state texture array indices, one per face direction.
    /// Indexed by StateId.Value in NativeAtlasLookup.
    ///
    /// Face direction convention:
    ///   0=PosX(East), 1=NegX(West), 2=PosY(Up), 3=NegY(Down), 4=PosZ(South), 5=NegZ(North)
    ///
    /// Overlay fields: 0xFFFF = no overlay for that face.
    ///
    /// Packed tint layout (ushort, 6 faces x 2 bits = 12 bits):
    ///   bits  0-1 : PosX, bits  2-3 : NegX, bits  4-5 : PosY,
    ///   bits  6-7 : NegY, bits  8-9 : PosZ, bits 10-11: NegZ
    ///   tintType: 0=none, 1=grass, 2=foliage, 3=water
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct AtlasEntry
    {
        /// <summary>Base texture atlas index for the +X (East) face.</summary>
        public ushort TexPosX;

        /// <summary>Base texture atlas index for the -X (West) face.</summary>
        public ushort TexNegX;

        /// <summary>Base texture atlas index for the +Y (Up) face.</summary>
        public ushort TexPosY;

        /// <summary>Base texture atlas index for the -Y (Down) face.</summary>
        public ushort TexNegY;

        /// <summary>Base texture atlas index for the +Z (South) face.</summary>
        public ushort TexPosZ;

        /// <summary>Base texture atlas index for the -Z (North) face.</summary>
        public ushort TexNegZ;

        /// <summary>Overlay texture atlas index for +X face (0xFFFF = no overlay).</summary>
        public ushort OvlPosX;

        /// <summary>Overlay texture atlas index for -X face (0xFFFF = no overlay).</summary>
        public ushort OvlNegX;

        /// <summary>Overlay texture atlas index for +Y face (0xFFFF = no overlay).</summary>
        public ushort OvlPosY;

        /// <summary>Overlay texture atlas index for -Y face (0xFFFF = no overlay).</summary>
        public ushort OvlNegY;

        /// <summary>Overlay texture atlas index for +Z face (0xFFFF = no overlay).</summary>
        public ushort OvlPosZ;

        /// <summary>Overlay texture atlas index for -Z face (0xFFFF = no overlay).</summary>
        public ushort OvlNegZ;

        /// <summary>Bit-packed base tint types for all 6 faces (2 bits each, 12 bits used).</summary>
        public ushort BaseTintPacked;

        /// <summary>Bit-packed overlay tint types for all 6 faces (2 bits each, 12 bits used).</summary>
        public ushort OverlayTintPacked;

        /// <summary>Returns the base texture atlas index for the given face direction (0-5).</summary>
        public ushort GetTextureIndex(int faceDirection)
        {
            return faceDirection switch
            {
                0 => TexPosX,
                1 => TexNegX,
                2 => TexPosY,
                3 => TexNegY,
                4 => TexPosZ,
                5 => TexNegZ,
                _ => 0,
            };
        }

        /// <summary>Returns the overlay texture atlas index for the given face direction (0xFFFF if none).</summary>
        public ushort GetOverlayTextureIndex(int faceDirection)
        {
            return faceDirection switch
            {
                0 => OvlPosX,
                1 => OvlNegX,
                2 => OvlPosY,
                3 => OvlNegY,
                4 => OvlPosZ,
                5 => OvlNegZ,
                _ => 0xFFFF,
            };
        }

        /// <summary>Extracts the 2-bit base tint type for the given face direction.</summary>
        public byte GetBaseTintType(int faceDirection)
        {
            return (byte)((BaseTintPacked >> (faceDirection * 2)) & 0x3);
        }

        /// <summary>Extracts the 2-bit overlay tint type for the given face direction.</summary>
        public byte GetOverlayTintType(int faceDirection)
        {
            return (byte)((OverlayTintPacked >> (faceDirection * 2)) & 0x3);
        }
    }
}
