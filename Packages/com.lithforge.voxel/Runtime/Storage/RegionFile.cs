using System;
using System.Collections.Generic;
using System.IO;

namespace Lithforge.Voxel.Storage
{
    public sealed class RegionFile : IDisposable
    {
        public const int RegionSize = 32;
        private const int _headerSize = RegionSize * RegionSize * 8; // 8 bytes per column (offset + size)
        private const int _sectorSize = 4096;

        private readonly string _filePath;
        private readonly Dictionary<int, byte[]> _cache = new Dictionary<int, byte[]>();
        private bool _disposed;

        public RegionFile(string filePath)
        {
            _filePath = filePath;
        }

        public bool HasChunk(int localX, int localZ)
        {
            int key = GetKey(localX, localZ);

            if (_cache.ContainsKey(key))
            {
                return true;
            }

            if (!File.Exists(_filePath))
            {
                return false;
            }

            using (FileStream fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read))
            using (BinaryReader reader = new BinaryReader(fs))
            {
                int headerOffset = key * 8;

                if (fs.Length < _headerSize)
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

            if (_cache.TryGetValue(key, out byte[] cached))
            {
                return cached;
            }

            if (!File.Exists(_filePath))
            {
                return null;
            }

            using (FileStream fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read))
            using (BinaryReader reader = new BinaryReader(fs))
            {
                if (fs.Length < _headerSize)
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

                return data;
            }
        }

        public void SaveChunk(int localX, int localZ, byte[] data)
        {
            int key = GetKey(localX, localZ);
            _cache[key] = data;
        }

        public void Flush()
        {
            if (_cache.Count == 0)
            {
                return;
            }

            // Read existing data or create new
            Dictionary<int, byte[]> existingData = new Dictionary<int, byte[]>();

            if (File.Exists(_filePath))
            {
                using (FileStream fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read))
                using (BinaryReader reader = new BinaryReader(fs))
                {
                    if (fs.Length >= _headerSize)
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

            // Merge cache into existing
            foreach (KeyValuePair<int, byte[]> kvp in _cache)
            {
                existingData[kvp.Key] = kvp.Value;
            }

            // Write entire file
            string dir = Path.GetDirectoryName(_filePath);

            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using (FileStream fs = new FileStream(_filePath, FileMode.Create, FileAccess.Write))
            using (BinaryWriter writer = new BinaryWriter(fs))
            {
                // Reserve header space
                writer.Write(new byte[_headerSize]);

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
            }

            _cache.Clear();
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
