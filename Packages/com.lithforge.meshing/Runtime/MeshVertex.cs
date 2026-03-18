using System.Runtime.InteropServices;

using Unity.Mathematics;

using UnityEngine.Rendering;

namespace Lithforge.Meshing
{
    /// <summary>
    ///     48-byte vertex for voxel meshes. 16-byte aligned (3 × 16).
    ///     Color channels:
    ///     r = AO (0-1), g = blockLight (0-1), b = sunLight (0-1), a = baseTexIndex (pure, no tint encoding)
    ///     TintOverlay packing (uint, 32 bits):
    ///     bits  0-1  : baseTintType     (0=none, 1=grass, 2=foliage, 3=water)
    ///     bits  2-3  : overlayTintType  (0=none, 1=grass, 2=foliage, 3=water)
    ///     bit   4    : hasOverlay       (0=no overlay, 1=has overlay)
    ///     bits  5-14 : overlayTexIndex  (0-1023, index into atlas array)
    ///     bits 15-31 : reserved (0)
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MeshVertex
    {
        public float3 Position;  // 12 bytes
        public float3 Normal;    // 12 bytes
        public half4 Color;      // 8 bytes
        public float2 UV;        // 8 bytes
        public uint TintOverlay; // 4 bytes
        public uint Pad;         // 4 bytes (16-byte alignment)

        public static readonly VertexAttributeDescriptor[] VertexAttributes =
        {
            new(VertexAttribute.Position),
            new(VertexAttribute.Normal),
            new(VertexAttribute.Color, VertexAttributeFormat.Float16, 4),
            new(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
            new(VertexAttribute.TexCoord1, VertexAttributeFormat.UInt32, 1),
            new(VertexAttribute.TexCoord2, VertexAttributeFormat.UInt32, 1),
        };
    }
}
