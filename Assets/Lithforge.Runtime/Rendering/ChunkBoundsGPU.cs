using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Lithforge.Runtime.Rendering
{
    /// <summary>
    /// Per-chunk metadata stored in a StructuredBuffer for GPU culling.
    /// Laid out as 2x float4 (32 bytes) for GPU cache alignment.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ChunkBoundsGPU
    {
        /// <summary>Minimum corner of the chunk AABB in world space (12 bytes).</summary>
        public float3 WorldMin;

        /// <summary>Padding to align WorldMin to 16 bytes for GPU cache alignment.</summary>
        public float Pad0;

        /// <summary>Maximum corner of the chunk AABB in world space (12 bytes).</summary>
        public float3 WorldMax;

        /// <summary>Padding to align WorldMax to 16 bytes for GPU cache alignment.</summary>
        public float Pad1;
    }
}
