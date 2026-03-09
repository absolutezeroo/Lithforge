using Lithforge.Voxel.Block;
using Unity.Mathematics;

namespace Lithforge.WorldGen.Decoration
{
    public struct PendingBlock
    {
        public int3 LocalPosition;
        public StateId State;
    }
}
