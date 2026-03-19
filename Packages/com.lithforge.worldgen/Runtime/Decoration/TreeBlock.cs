using Lithforge.Voxel.Block;
using Unity.Mathematics;

namespace Lithforge.WorldGen.Decoration
{
    /// <summary>Single block within a tree template, defined as an offset from the trunk base.</summary>
    public struct TreeBlock
    {
        /// <summary>Position offset relative to the tree base (0, 0, 0).</summary>
        public int3 Offset;

        /// <summary>Block state to place (log or leaves).</summary>
        public StateId State;
    }
}
