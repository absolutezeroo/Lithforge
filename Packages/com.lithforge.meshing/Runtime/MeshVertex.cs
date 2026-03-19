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
        /// <summary>World-space vertex position.</summary>
        public float3 Position;  // 12 bytes

        /// <summary>Face normal direction.</summary>
        public float3 Normal;    // 12 bytes

        /// <summary>r=AO, g=blockLight, b=sunLight, a=baseTexIndex (all 0-1).</summary>
        public half4 Color;      // 8 bytes

        /// <summary>Texture UV coordinates.</summary>
        public float2 UV;        // 8 bytes

        /// <summary>Bit-packed base tint, overlay tint, hasOverlay flag, and overlay texture index.</summary>
        public uint TintOverlay; // 4 bytes

        /// <summary>Unused padding for 16-byte alignment.</summary>
        public uint Pad;         // 4 bytes (16-byte alignment)

        /// <summary>Vertex attribute descriptors for Mesh API upload.</summary>
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
