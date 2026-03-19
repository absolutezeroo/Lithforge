using Lithforge.Voxel.Block;
using Unity.Mathematics;

namespace Lithforge.WorldGen.Decoration
{
    /// <summary>Deferred block placement queued for a neighboring chunk that has not yet generated.</summary>
    public struct PendingBlock
    {
        /// <summary>Chunk-local position where the block should be placed.</summary>
        public int3 LocalPosition;

        /// <summary>Block state to write when the target chunk generates.</summary>
        public StateId State;
    }
}
