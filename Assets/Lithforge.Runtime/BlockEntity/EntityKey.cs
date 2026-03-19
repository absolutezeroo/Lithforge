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
        /// <summary>Chunk coordinate in world space.</summary>
        public int3 ChunkCoord;

        /// <summary>Flat voxel index within the chunk (0 to ChunkSize^3 - 1).</summary>
        public int FlatIndex;

        /// <summary>Creates an EntityKey from a chunk coordinate and flat voxel index.</summary>
        public EntityKey(int3 chunkCoord, int flatIndex)
        {
            ChunkCoord = chunkCoord;
            FlatIndex = flatIndex;
        }

        /// <summary>Returns true if both chunk coordinate and flat index match.</summary>
        public bool Equals(EntityKey other)
        {
            return ChunkCoord.Equals(other.ChunkCoord) && FlatIndex == other.FlatIndex;
        }

        /// <summary>Returns true if the object is an EntityKey with matching fields.</summary>
        public override bool Equals(object obj)
        {
            return obj is EntityKey other && Equals(other);
        }

        /// <summary>Computes a hash combining chunk coordinate and flat index for dictionary use.</summary>
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
