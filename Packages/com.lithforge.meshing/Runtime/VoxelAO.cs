using System.Runtime.CompilerServices;

namespace Lithforge.Meshing
{
    /// <summary>
    /// Burst-compatible ambient occlusion calculation for voxel vertices.
    /// Uses the classic 3-neighbor method: two sides and one corner.
    /// Returns AO level 0 (fully occluded) to 3 (fully unoccluded).
    /// </summary>
    public static class VoxelAO
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Compute(bool side1, bool side2, bool corner)
        {
            if (side1 && side2)
            {
                return 0;
            }

            int occluders = (side1 ? 1 : 0) + (side2 ? 1 : 0) + (corner ? 1 : 0);
            return (byte)(3 - occluders);
        }
    }
}
