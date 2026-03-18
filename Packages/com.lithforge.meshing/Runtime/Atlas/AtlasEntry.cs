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
        // Base texture indices per face
        public ushort TexPosX;
        public ushort TexNegX;
        public ushort TexPosY;
        public ushort TexNegY;
        public ushort TexPosZ;
        public ushort TexNegZ;

        // Overlay texture indices per face (0xFFFF = no overlay)
        public ushort OvlPosX;
        public ushort OvlNegX;
        public ushort OvlPosY;
        public ushort OvlNegY;
        public ushort OvlPosZ;
        public ushort OvlNegZ;

        // Packed per-face base tint type
        public ushort BaseTintPacked;

        // Packed per-face overlay tint type
        public ushort OverlayTintPacked;

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

        public byte GetBaseTintType(int faceDirection)
        {
            return (byte)((BaseTintPacked >> (faceDirection * 2)) & 0x3);
        }

        public byte GetOverlayTintType(int faceDirection)
        {
            return (byte)((OverlayTintPacked >> (faceDirection * 2)) & 0x3);
        }
    }
}
