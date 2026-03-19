using Unity.Mathematics;

namespace Lithforge.Network.Server
{
    /// <summary>
    /// Provides server-side chunk data access for network streaming.
    /// Implemented by Tier 3 to expose chunk serialization and readiness queries
    /// to <see cref="ServerGameLoop"/> and <see cref="ChunkStreamingManager"/>
    /// without requiring direct references to ChunkManager internals.
    /// </summary>
    public interface IServerChunkProvider
    {
        /// <summary>
        /// Returns true if the chunk at the given coordinate is loaded and in Ready state.
        /// </summary>
        public bool IsChunkReady(int3 coord);

        /// <summary>
        /// Serializes the full chunk data at the given coordinate for network transmission.
        /// Returns null if the chunk is not ready. Uses ChunkNetSerializer.SerializeFullChunk
        /// internally with the chunk's voxel and light data.
        /// </summary>
        public byte[] SerializeChunk(int3 coord);

        /// <summary>
        /// Finds a safe Y coordinate for spawning at the given world XZ position.
        /// Scans downward through the specified chunk Y range.
        /// Returns fallbackY if no solid ground is found.
        /// </summary>
        public int FindSafeSpawnY(int worldX, int worldZ, int chunkYMin, int chunkYMax, int fallbackY);

        /// <summary>
        /// Returns true if the chunk at the given coordinate has completed generation
        /// (state >= Generated) and has valid voxel data. Used by LocalChunkStreamingStrategy
        /// for zero-copy local chunk delivery.
        /// </summary>
        public bool IsChunkGenerated(int3 coord);

        /// <summary>
        /// Returns true if the chunk at the given coordinate has completed generation
        /// and contains only air voxels. All-air chunks skip meshing and streaming
        /// and count as immediately ready for the spawn-readiness gate.
        /// </summary>
        public bool IsChunkAllAir(int3 coord);

        /// <summary>
        /// Returns the network version counter for the chunk at the given coordinate.
        /// The version increments on each block edit. Returns -1 if the chunk is not loaded.
        /// Used by <see cref="CompressedChunkCache"/> to detect stale cache entries.
        /// </summary>
        public int GetChunkNetworkVersion(int3 coord);
    }
}
