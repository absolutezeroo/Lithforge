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
        public float3 WorldMin;   // 12 bytes
        public float Pad0;        // 4 bytes (align to 16)
        public float3 WorldMax;   // 12 bytes
        public float Pad1;        // 4 bytes (align to 16)
    }
}
