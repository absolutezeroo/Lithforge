using Unity.Collections;
using Unity.Mathematics;

namespace Lithforge.Physics
{
    /// <summary>
    /// Burst-compatible block solidity lookup. Replaces Func&lt;int3, bool&gt; delegate
    /// for use in jobified collision resolution.
    ///
    /// Contains a pre-filled NativeHashMap of solidity values for the broad-phase region.
    /// For single-entity main-thread use: construct before Resolve, dispose after.
    /// For future jobified multi-entity use: pass as [ReadOnly] field in a job struct.
    /// </summary>
    public struct SolidBlockQuery
    {
        /// <summary>Pre-filled solidity lookup for the broad-phase region around an entity.</summary>
        [ReadOnly] public NativeHashMap<int3, bool> SolidMap;

        /// <summary>
        /// Returns true if the block at the given coordinate is solid.
        /// Coordinates not in the map are treated as solid (conservative fallback
        /// matching the unloaded-chunk-is-solid convention).
        /// </summary>
        public bool IsSolid(int3 coord)
        {
            if (SolidMap.TryGetValue(coord, out bool solid))
            {
                return solid;
            }

            return true;
        }
    }
}
