using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Lithforge.Runtime.Player
{
    /// <summary>
    /// Vertex format for the player model mesh. Stored in a StructuredBuffer
    /// and read by the player model shader via SV_VertexID.
    /// 40 bytes, 8-byte aligned. Separate from PackedMeshVertex because the player
    /// model needs float positions relative to part pivots, normals, UVs, and a bone index,
    /// none of which overlap with the voxel vertex encoding.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PlayerModelVertex
    {
        /// <summary>Local position relative to the part pivot (model space).</summary>
        public float3 Position;

        /// <summary>Local face normal (model space).</summary>
        public float3 Normal;

        /// <summary>Skin texture UV in [0,1] normalized coordinates.</summary>
        public float2 UV;

        /// <summary>Bone index (0=head, 1=body, 2=rightArm, 3=leftArm, 4=rightLeg, 5=leftLeg).</summary>
        public uint PartID;

        /// <summary>Bit flags. Bit 0: isOverlay (inflated mesh for sleeve/hat layer).</summary>
        public uint Flags;
    }
}
