using System;
using System.Collections.Generic;
using System.IO;

namespace Lithforge.Voxel.Storage
{
    /// <summary>
    ///     Read-through cache for a single region file on disk. A region covers 32x32 chunks
    ///     in XZ. Chunk data is cached in memory on SaveChunk and flushed atomically on Flush.
    ///     Flush uses write-to-temp + rename to prevent corruption on crash.
    /// </summary>
    public sealed class RegionFile : IDisposable
    {
        /// <summary>Number of chunks per axis in a region (32x32 XZ grid).</summary>
        public const int RegionSize = 32;

        /// <summary>Header size in bytes (32*32 entries * 8 bytes per entry = 8192).</summary>
        private const int HeaderSize = RegionSize * RegionSize * 8;

        /// <summary>Minimum allocation unit (unused in current implementation but reserved).</summary>
        private const int SectorSize = 4096;

        /// <summary>In-memory cache of serialized chunk data, keyed by local index (z*32+x).</summary>
        private readonly Dictionary<int, byte[]> _cache = new();

        /// <summary>Lock protecting _cache for thread-safe access from AsyncChunkSaver.</summary>
        private readonly object _cacheLock = new();

        /// <summary>Full filesystem path to this region file (.lfrg).</summary>
        private readonly string _filePath;

        /// <summary>Whether this region file has been disposed.</summary>
        private bool _disposed;

        /// <summary>Creates a region file handle for the given file path (file need not exist yet).</summary>
        public RegionFile(string filePath)
        {
            _filePath = filePath;
        }

        /// <summary>True if the cache contains data that has not been flushed to disk.</summary>
        public bool IsDirty { get; private set; }

        /// <summary>Flushes cached data to disk and marks the region as disposed.</summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                Flush();
            }
        }

        /// <summary>Returns true if data exists for the chunk at the given local XZ position (cache or disk).</summary>
        public bool HasChunk(int localX, int localZ)
        {
            int key = GetKey(localX, localZ);

            lock (_cacheLock)
            {
                if (_cache.ContainsKey(key))
                {
                    return true;
                }
            }

            if (!File.Exists(_filePath))
            {
                return false;
            }

            using (FileStream fs = new(_filePath, FileMode.Open, FileAccess.Read))
            using (BinaryReader reader = new(fs))
            {
                int headerOffset = key * 8;

                if (fs.Length < HeaderSize)
                {
                    return false;
                }

                fs.Seek(headerOffset, SeekOrigin.Begin);
                int dataOffset = reader.ReadInt32();
                int dataSize = reader.ReadInt32();

                return dataOffset > 0 && dataSize > 0;
            }
        }

        /// <summary>Loads serialized chunk data from cache or disk. Returns null if not found.</summary>
        public byte[] LoadChunk(int localX, int localZ)
        {
            int key = GetKey(localX, localZ);

            lock (_cacheLock)
            {
                if (_cache.TryGetValue(key, out byte[] cached))
                {
                    return cached;
                }
            }

            if (!File.Exists(_filePath))
            {
                return null;
            }

            using (FileStream fs = new(_filePath, FileMode.Open, FileAccess.Read))
            using (BinaryReader reader = new(fs))
            {
                if (fs.Length < HeaderSize)
                {
                    return null;
                }

                int headerOffset = key * 8;
                fs.Seek(headerOffset, SeekOrigin.Begin);
                int dataOffset = reader.ReadInt32();
                int dataSize = reader.ReadInt32();

                if (dataOffset <= 0 || dataSize <= 0)
                {
                    return null;
                }

                fs.Seek(dataOffset, SeekOrigin.Begin);
                byte[] data = reader.ReadBytes(dataSize);

                lock (_cacheLock)
                {
                    _cache[key] = data;
                }

                return data;
            }
        }

        /// <summary>Stores serialized chunk data in the cache and marks the region dirty.</summary>
        public void SaveChunk(int localX, int localZ, byte[] data)
        {
            int key = GetKey(localX, localZ);

            lock (_cacheLock)
            {
                _cache[key] = data;
                IsDirty = true;
            }
        }

        /// <summary>
        ///     Atomically writes all cached chunk data to disk. Reads existing data from
        ///     the file, merges cached updates, writes to a .tmp file, then renames to
        ///     replace the original. Removes flushed entries from the cache.
        /// </summary>
        public void Flush()
        {
            Dictionary<int, byte[]> snapshot;

            lock (_cacheLock)
            {
                if (_cache.Count == 0)
                {
                    return;
                }

                snapshot = new Dictionary<int, byte[]>(_cache);
            }

            // Read existing data or create new
            Dictionary<int, byte[]> existingData = new();

            if (File.Exists(_filePath))
            {
                using (FileStream fs = new(_filePath, FileMode.Open, FileAccess.Read))
                using (BinaryReader reader = new(fs))
                {
                    if (fs.Length >= HeaderSize)
                    {
                        for (int i = 0; i < RegionSize * RegionSize; i++)
                        {
                            fs.Seek(i * 8, SeekOrigin.Begin);
                            int dataOffset = reader.ReadInt32();
                            int dataSize = reader.ReadInt32();

                            if (dataOffset > 0 && dataSize > 0)
                            {
                                fs.Seek(dataOffset, SeekOrigin.Begin);
                                existingData[i] = reader.ReadBytes(dataSize);
                            }
                        }
                    }
                }
            }

            // Merge snapshot into existing
            foreach (KeyValuePair<int, byte[]> kvp in snapshot)
            {
                existingData[kvp.Key] = kvp.Value;
            }

            // Atomic write: write to temp file, then rename to replace the original.
            // This prevents corruption if a crash occurs during write.
            string dir = Path.GetDirectoryName(_filePath);

            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string tempPath = _filePath + ".tmp";

            try
            {
                using (FileStream fs = new(tempPath, FileMode.Create, FileAccess.Write))
                using (BinaryWriter writer = new(fs))
                {
                    // Reserve header space
                    writer.Write(new byte[HeaderSize]);

                    // Write data and track offsets
                    int[] offsets = new int[RegionSize * RegionSize];
                    int[] sizes = new int[RegionSize * RegionSize];

                    foreach (KeyValuePair<int, byte[]> kvp in existingData)
                    {
                        offsets[kvp.Key] = (int)fs.Position;
                        sizes[kvp.Key] = kvp.Value.Length;
                        writer.Write(kvp.Value);
                    }

                    // Write header
                    fs.Seek(0, SeekOrigin.Begin);

                    for (int i = 0; i < RegionSize * RegionSize; i++)
                    {
                        writer.Write(offsets[i]);
                        writer.Write(sizes[i]);
                    }

                    fs.Flush();
                }

                // Backup rotation: keep the previous version as .bak
                if (File.Exists(_filePath))
                {
                    string backupPath = _filePath + ".bak";

                    if (File.Exists(backupPath))
                    {
                        File.Delete(backupPath);
                    }

                    File.Move(_filePath, backupPath);
                }

                // Atomic rename: replaces original (atomic on most OS)
                File.Move(tempPath, _filePath);

                // Remove only snapshotted entries from cache.
                // If a concurrent SaveChunk wrote a new value for the same key,
                // keep the new value (ReferenceEquals will be false).
                lock (_cacheLock)
                {
                    foreach (KeyValuePair<int, byte[]> kvp in snapshot)
                    {
                        if (_cache.TryGetValue(kvp.Key, out byte[] current)
                            && ReferenceEquals(current, kvp.Value))
                        {
                            _cache.Remove(kvp.Key);
                        }
                    }

                    IsDirty = _cache.Count > 0;
                }
            }
            catch
            {
                // Write failed — delete temp file, leave original untouched
                if (File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch
                    {
                        // Best effort cleanup
                    }
                }

                throw;
            }

        }

        /// <summary>Computes the flat cache key from local XZ coordinates (z * RegionSize + x).</summary>
        private static int GetKey(int localX, int localZ)
        {
            return localZ * RegionSize + localX;
        }
    }
}
