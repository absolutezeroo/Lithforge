using System.Collections.Generic;

using Unity.Mathematics;

namespace Lithforge.Network.Chunk
{
    /// <summary>
    /// Caches serialized (zstd-compressed) chunk byte arrays keyed by chunk coordinate.
    /// Each entry stores the <see cref="ManagedChunk.NetworkVersion"/> at serialization time.
    /// When the chunk's version advances (block edits), the cache entry is stale and
    /// <see cref="TryGet"/> returns false, prompting re-serialization.
    /// Thread-safety: main-thread only (same thread as ServerGameLoop).
    /// </summary>
    public sealed class CompressedChunkCache
    {
        /// <summary>Cached serialized bytes and the version they were produced at.</summary>
        private struct CacheEntry
        {
            /// <summary>Zstd-compressed chunk payload from ChunkNetSerializer.</summary>
            public byte[] Data;

            /// <summary>ManagedChunk.NetworkVersion at the time of serialization.</summary>
            public int Version;
        }

        /// <summary>
        /// Maps chunk coordinates to their cached serialized data and version.
        /// </summary>
        private readonly Dictionary<int3, CacheEntry> _cache = new();

        /// <summary>Number of cache entries currently stored.</summary>
        public int Count
        {
            get { return _cache.Count; }
        }

        /// <summary>
        /// Attempts to retrieve cached serialized bytes for the given chunk coordinate.
        /// Returns true only if the cache contains an entry whose version matches
        /// <paramref name="currentVersion"/>, indicating the cached data is still valid.
        /// </summary>
        public bool TryGet(int3 coord, int currentVersion, out byte[] data)
        {
            if (_cache.TryGetValue(coord, out CacheEntry entry) && entry.Version == currentVersion)
            {
                data = entry.Data;

                return true;
            }

            data = null;

            return false;
        }

        /// <summary>
        /// Stores serialized chunk bytes in the cache, tagged with the chunk's current version.
        /// Overwrites any existing entry for the same coordinate.
        /// </summary>
        public void Put(int3 coord, int version, byte[] data)
        {
            _cache[coord] = new CacheEntry
            {
                Data = data,
                Version = version,
            };
        }

        /// <summary>
        /// Removes the cache entry for the given coordinate, if present.
        /// Called when a chunk is unloaded to avoid retaining stale data.
        /// </summary>
        public void Remove(int3 coord)
        {
            _cache.Remove(coord);
        }

        /// <summary>
        /// Removes all cache entries. Called on session teardown.
        /// </summary>
        public void Clear()
        {
            _cache.Clear();
        }
    }
}
