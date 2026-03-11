using System.Collections.Generic;
using Unity.Mathematics;

namespace Lithforge.Voxel.Chunk
{
    internal struct ChunkDistanceComparer : IComparer<int3>
    {
        public int3 Center;

        public int Compare(int3 a, int3 b)
        {
            float distA = math.lengthsq(a - Center);
            float distB = math.lengthsq(b - Center);
            return distA.CompareTo(distB);
        }
    }
}