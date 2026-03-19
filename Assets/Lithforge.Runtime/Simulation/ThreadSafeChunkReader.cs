using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;

using Unity.Mathematics;

namespace Lithforge.Runtime.Simulation
{
    /// <summary>
    ///     Thread-safe implementation of <see cref="IChunkDataReader" /> backed by
    ///     <see cref="ChunkManager" />. Safe to call from the server background thread
    ///     for all player-adjacent chunks because <see cref="ChunkManager.AdjustRefCounts" />
    ///     ensures RefCount &gt; 0 (preventing disposal) for every chunk in any player's
    ///     interest radius. <see cref="GetFluidLevel" /> reads liquid data without calling
    ///     <c>JobHandle.Complete()</c>, accepting a possible stale-by-one-tick fluid value
    ///     on the server thread.
    /// </summary>
    public sealed class ThreadSafeChunkReader : IChunkDataReader
    {
        /// <summary>The chunk manager whose NativeArray data is read cross-thread.</summary>
        private readonly ChunkManager _chunkManager;

        /// <summary>Creates a reader backed by the given chunk manager.</summary>
        public ThreadSafeChunkReader(ChunkManager chunkManager)
        {
            _chunkManager = chunkManager;
        }

        /// <summary>
        ///     Returns the <see cref="StateId" /> at the given world-space coordinate.
        ///     Delegates to <see cref="ChunkManager.GetBlock" /> which guards
        ///     <c>Data.IsCreated</c> internally.
        /// </summary>
        public StateId GetBlock(int3 worldCoord)
        {
            return _chunkManager.GetBlock(worldCoord);
        }

        /// <summary>
        ///     Returns true if the chunk containing the given coordinate is loaded with valid data.
        ///     Delegates to <see cref="ChunkManager.IsBlockLoaded" /> which guards
        ///     <c>Data.IsCreated</c> internally.
        /// </summary>
        public bool IsBlockLoaded(int3 worldCoord)
        {
            return _chunkManager.IsBlockLoaded(worldCoord);
        }

        /// <summary>
        ///     Returns the liquid cell byte at the given world-space coordinate without calling
        ///     <c>JobHandle.Complete()</c> on any in-flight liquid job. Safe for background
        ///     thread reads — single byte writes on x86/ARM are atomic, so no torn value is
        ///     possible. Returns 0 if the chunk is not loaded or <c>LiquidData</c> is not created.
        /// </summary>
        public byte GetFluidLevel(int3 worldCoord)
        {
            int3 chunkCoord = ChunkManager.WorldToChunk(worldCoord);
            ManagedChunk chunk = _chunkManager.GetChunk(chunkCoord);

            if (chunk is null || !chunk.LiquidData.IsCreated)
            {
                return 0;
            }

            int localX = worldCoord.x - chunkCoord.x * ChunkConstants.Size;
            int localY = worldCoord.y - chunkCoord.y * ChunkConstants.Size;
            int localZ = worldCoord.z - chunkCoord.z * ChunkConstants.Size;
            int index = ChunkData.GetIndex(localX, localY, localZ);

            return chunk.LiquidData[index];
        }
    }
}
