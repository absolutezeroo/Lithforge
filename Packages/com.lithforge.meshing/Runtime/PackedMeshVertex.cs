using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Lithforge.Meshing
{
    /// <summary>
    /// 16-byte compressed voxel vertex. All data packed into 4 uint32 words.
    /// Decoded in the vertex shader via StructuredBuffer fetch.
    ///
    /// word0: posX(6) | posY(6) | posZ(6) | normal(3) | ao(2) | blockLight(4) | sunLight(4) | fluidTop(1)
    ///   bits  0-5 : posX        (0-63, local grid position; range 0-32 for LOD0)
    ///   bits  6-11: posY        (0-63, local grid position; range 0-32 for LOD0)
    ///   bits 12-17: posZ        (0-63, local grid position; range 0-32 for LOD0)
    ///   bits 18-20: normal      (0-5: +X, -X, +Y, -Y, +Z, -Z)
    ///   bits 21-22: ao          (0-3: 0=fully occluded, 3=unoccluded)
    ///   bits 23-26: blockLight  (0-15)
    ///   bits 27-30: sunLight    (0-15)
    ///   bit  31:    fluidTop    (1=fluid top face, shader applies -0.125 Y offset)
    ///
    /// word1: texIndex(10) | baseTintType(2) | hasOverlay(1) | overlayTexIndex(10) | overlayTintType(2) | lodScale(2) | pad(5)
    ///   bits  0-9 : texIndex        (0-1023, base texture atlas index)
    ///   bits 10-11: baseTintType    (0=none, 1=grass, 2=foliage, 3=water)
    ///   bit  12:    hasOverlay      (0=no, 1=yes)
    ///   bits 13-22: overlayTexIndex (0-1023, overlay atlas index)
    ///   bits 23-24: overlayTintType (0=none, 1=grass, 2=foliage, 3=water)
    ///   bits 25-26: lodScale        (0=x1, 1=x2, 2=x4, 3=x8)
    ///   bits 27-31: padding
    ///
    /// word2: uvX(8) | uvY(8) | chunkWorldX(16)
    ///   bits  0-7 : uvX             (0-255, greedy quad width in voxels)
    ///   bits  8-15: uvY             (0-255, greedy quad height in voxels)
    ///   bits 16-31: chunkWorldX     (int16, coord.x * ChunkSize)
    ///
    /// word3: chunkWorldY(16) | chunkWorldZ(16)
    ///   bits  0-15: chunkWorldY     (int16, coord.y * ChunkSize)
    ///   bits 16-31: chunkWorldZ     (int16, coord.z * ChunkSize)
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PackedMeshVertex
    {
        public uint Word0;
        public uint Word1;
        public uint Word2;
        public uint Word3;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PackedMeshVertex Pack(
            int posX, int posY, int posZ,
            int normalIndex,
            int ao,
            int blockLight,
            int sunLight,
            bool fluidTop,
            int texIndex,
            int baseTintType,
            bool hasOverlay,
            int overlayTexIndex,
            int overlayTintType,
            int lodScale,
            int uvX, int uvY,
            int chunkWorldX,
            int chunkWorldY,
            int chunkWorldZ)
        {
            PackedMeshVertex v;

            v.Word0 = ((uint)(posX & 0x3F))
                    | ((uint)(posY & 0x3F) << 6)
                    | ((uint)(posZ & 0x3F) << 12)
                    | ((uint)(normalIndex & 0x7) << 18)
                    | ((uint)(ao & 0x3) << 21)
                    | ((uint)(blockLight & 0xF) << 23)
                    | ((uint)(sunLight & 0xF) << 27)
                    | ((uint)(fluidTop ? 1u : 0u) << 31);

            v.Word1 = ((uint)(texIndex & 0x3FF))
                    | ((uint)(baseTintType & 0x3) << 10)
                    | ((uint)(hasOverlay ? 1u : 0u) << 12)
                    | ((uint)(overlayTexIndex & 0x3FF) << 13)
                    | ((uint)(overlayTintType & 0x3) << 23)
                    | ((uint)(lodScale & 0x3) << 25);

            v.Word2 = ((uint)(uvX & 0xFF))
                    | ((uint)(uvY & 0xFF) << 8)
                    | ((uint)((ushort)chunkWorldX) << 16);

            v.Word3 = ((uint)((ushort)chunkWorldY))
                    | ((uint)((ushort)chunkWorldZ) << 16);

            return v;
        }
    }
}
