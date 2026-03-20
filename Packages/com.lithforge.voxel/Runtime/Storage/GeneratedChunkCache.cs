using System.Collections.Generic;

using Unity.Mathematics;

namespace Lithforge.Voxel.Storage
{
    /// <summary>
    ///     Bounded LRU cache of serialized chunk bytes for unmodified chunks.
    ///     When a chunk with IsDirty=false is evicted from the active set, its serialized
    ///     bytes are stored here so that re-entering the view distance does not require
    ///     a full worldgen pass. Bounded at <see cref="Capacity" /> entries.
    ///     Thread-safety: main-thread only.
    /// </summary>
    public sealed class GeneratedChunkCache
    {
        /// <summary>Default cache capacity (1024 entries, ~60 MB at ~60 KB average chunk size).</summary>
        public const int DefaultCapacity = 1024;

        /// <summary>Maximum number of entries before LRU eviction.</summary>
        private readonly int _capacity;

        /// <summary>Lookup table: chunk coordinate to linked list node for O(1) access.</summary>
        private readonly Dictionary<int3, LinkedListNode<CacheEntry>> _lookup;

        /// <summary>Eviction order: most recently used at Back, least recently used at Front.</summary>
        private readonly LinkedList<CacheEntry> _order;

        /// <summary>Creates a cache with the given capacity.</summary>
        public GeneratedChunkCache(int capacity = DefaultCapacity)
        {
            _capacity = capacity;
            _lookup = new Dictionary<int3, LinkedListNode<CacheEntry>>(capacity + 4);
            _order = new LinkedList<CacheEntry>();
        }

        /// <summary>Maximum number of entries this cache can hold.</summary>
        public int Capacity
        {
            get { return _capacity; }
        }

        /// <summary>Number of entries currently in the cache.</summary>
        public int Count
        {
            get { return _lookup.Count; }
        }

        /// <summary>
        ///     Attempts to retrieve cached bytes for the given chunk coordinate.
        ///     Moves the entry to the back (most-recently-used) on hit.
        ///     Returns false if the coordinate is not cached.
        /// </summary>
        public bool TryGet(int3 coord, out byte[] data)
        {
            if (!_lookup.TryGetValue(coord, out LinkedListNode<CacheEntry> node))
            {
                data = null;
                return false;
            }

            // Promote to MRU end
            _order.Remove(node);
            _order.AddLast(node);
            data = node.Value.Data;
            return true;
        }

        /// <summary>
        ///     Inserts or replaces the serialized bytes for the given chunk coordinate.
        ///     Evicts the least-recently-used entry when at capacity.
        /// </summary>
        public void Put(int3 coord, byte[] data)
        {
            if (_lookup.TryGetValue(coord, out LinkedListNode<CacheEntry> existing))
            {
                existing.Value = new CacheEntry { Coord = coord, Data = data };
                _order.Remove(existing);
                _order.AddLast(existing);
                return;
            }

            if (_lookup.Count >= _capacity)
            {
                EvictLru();
            }

            LinkedListNode<CacheEntry> node = _order.AddLast(
                new CacheEntry { Coord = coord, Data = data });
            _lookup[coord] = node;
        }

        /// <summary>Removes the entry for the given coordinate, if present.</summary>
        public void Remove(int3 coord)
        {
            if (_lookup.TryGetValue(coord, out LinkedListNode<CacheEntry> node))
            {
                _lookup.Remove(coord);
                _order.Remove(node);
            }
        }

        /// <summary>Removes all entries.</summary>
        public void Clear()
        {
            _lookup.Clear();
            _order.Clear();
        }

        /// <summary>Evicts the least-recently-used entry (front of the linked list).</summary>
        private void EvictLru()
        {
            LinkedListNode<CacheEntry> lru = _order.First;

            if (lru is null)
            {
                return;
            }

            _lookup.Remove(lru.Value.Coord);
            _order.RemoveFirst();
        }

        /// <summary>Cache entry pairing a chunk coordinate with its serialized bytes.</summary>
        private struct CacheEntry
        {
            /// <summary>Chunk coordinate key for reverse-lookup during eviction.</summary>
            public int3 Coord;

            /// <summary>Serialized chunk bytes (palette + compressed voxel + light + block entities + CRC32).</summary>
            public byte[] Data;
        }
    }
}
