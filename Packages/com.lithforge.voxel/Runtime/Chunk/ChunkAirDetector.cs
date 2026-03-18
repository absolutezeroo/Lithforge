using Lithforge.Voxel.Block;

using Unity.Collections;

namespace Lithforge.Voxel.Chunk
{
    /// <summary>
    ///     Utility for detecting chunks that contain only air voxels.
    ///     All-air chunks skip meshing and count as immediately ready for
    ///     the spawn-readiness gate and chunk streaming.
    /// </summary>
    public static class ChunkAirDetector
    {
        /// <summary>
        ///     Returns true if every voxel in the data array is air (StateId == 0).
        ///     O(N) scan where N = ChunkConstants.Volume (32768).
        /// </summary>
        public static bool IsAllAir(NativeArray<StateId> data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i].Value != 0)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
