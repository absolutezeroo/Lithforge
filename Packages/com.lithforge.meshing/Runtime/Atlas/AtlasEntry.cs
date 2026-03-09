using System.Runtime.InteropServices;

namespace Lithforge.Meshing.Atlas
{
    /// <summary>
    /// Blittable per-state texture array indices, one per face direction.
    /// Indexed by StateId.Value in NativeAtlasLookup.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct AtlasEntry
    {
        public ushort TexPosX;
        public ushort TexNegX;
        public ushort TexPosY;
        public ushort TexNegY;
        public ushort TexPosZ;
        public ushort TexNegZ;

        public ushort GetTextureIndex(int faceDirection)
        {
            switch (faceDirection)
            {
                case 0: return TexPosX;
                case 1: return TexNegX;
                case 2: return TexPosY;
                case 3: return TexNegY;
                case 4: return TexPosZ;
                case 5: return TexNegZ;
                default: return 0;
            }
        }
    }
}
