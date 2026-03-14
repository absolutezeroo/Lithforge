using System;
using Unity.Mathematics;

namespace Lithforge.Runtime.BlockEntity
{
    /// <summary>
    /// Composite key identifying a block entity by chunk coordinate + flat voxel index.
    /// Used as dictionary key in BlockEntityTickScheduler.
    /// </summary>
    public struct EntityKey : IEquatable<EntityKey>
    {
        public int3 ChunkCoord;
        public int FlatIndex;

        public EntityKey(int3 chunkCoord, int flatIndex)
        {
            ChunkCoord = chunkCoord;
            FlatIndex = flatIndex;
        }

        public bool Equals(EntityKey other)
        {
            return ChunkCoord.Equals(other.ChunkCoord) && FlatIndex == other.FlatIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is EntityKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ChunkCoord.x * 73856093;
                hash ^= ChunkCoord.y * 19349663;
                hash ^= ChunkCoord.z * 83492791;
                hash ^= FlatIndex * 397;

                return hash;
            }
        }
    }
}
