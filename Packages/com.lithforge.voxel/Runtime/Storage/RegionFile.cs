using System;
using System.Collections.Generic;
using System.IO;

namespace Lithforge.Voxel.Storage
{
    public sealed class RegionFile : IDisposable
    {
        public const int RegionSize = 32;
        private const int HeaderSize = RegionSize * RegionSize * 8; // 8 bytes per column (offset + size)
        private const int SectorSize = 4096;

        private readonly string _filePath;
        private readonly Dictionary<int, byte[]> _cache = new();
        private readonly object _cacheLock = new();
        private bool _disposed;
        private bool _isDirty;

        public RegionFile(string filePath)
        {
            _filePath = filePath;
        }

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

        public bool IsDirty
        {
            get { return _isDirty; }
        }

        public void SaveChunk(int localX, int localZ, byte[] data)
        {
            int key = GetKey(localX, localZ);

            lock (_cacheLock)
            {
                _cache[key] = data;
                _isDirty = true;
            }
        }

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

                    _isDirty = _cache.Count > 0;
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

        private static int GetKey(int localX, int localZ)
        {
            return localZ * RegionSize + localX;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                Flush();
            }
        }
    }
}
