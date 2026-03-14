using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Lithforge.Runtime.Player
{
    /// <summary>
    /// Vertex format for held items rendered in first-person view.
    /// Uses the same StructuredBuffer approach as the arm mesh but with
    /// atlas texture coordinates instead of skin UVs.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct HeldItemVertex
    {
        /// <summary>Position in model space (relative to hand locator).</summary>
        public float3 Position;

        /// <summary>Face normal.</summary>
        public float3 Normal;

        /// <summary>UV coordinates for the texture atlas or item sprite.</summary>
        public float2 UV;

        /// <summary>Texture atlas index (for block items) or 0 (for flat items).</summary>
        public uint TexIndex;

        /// <summary>Reserved for future use.</summary>
        public uint Padding;
    }
}
