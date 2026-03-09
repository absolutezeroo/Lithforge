using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Unity.Mathematics;

namespace Lithforge.Runtime.Input
{
    /// <summary>
    /// Shared utility for checking whether a voxel at a world coordinate is solid.
    /// Used by both PlayerController and BlockInteraction to avoid code duplication.
    /// </summary>
    internal static class SolidBlockQuery
    {
        public static bool IsSolid(
            int3 worldCoord,
            ChunkManager chunkManager,
            NativeStateRegistry nativeStateRegistry)
        {
            StateId stateId = chunkManager.GetBlock(worldCoord);
            BlockStateCompact compact = nativeStateRegistry.States[stateId.Value];

            return compact.CollisionShape != 0;
        }
    }
}
