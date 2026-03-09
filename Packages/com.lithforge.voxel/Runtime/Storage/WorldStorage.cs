using System;
using System.Collections.Generic;
using System.IO;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Unity.Collections;
using Unity.Mathematics;

namespace Lithforge.Voxel.Storage
{
    public sealed class WorldStorage : IDisposable
    {
        private readonly string _worldDir;
        private readonly string _regionDir;
        private readonly Dictionary<int2, RegionFile> _regionFiles = new Dictionary<int2, RegionFile>();
        private bool _disposed;

        public WorldStorage(string worldDir)
        {
            _worldDir = worldDir;
            _regionDir = Path.Combine(worldDir, "region");

            if (!Directory.Exists(_regionDir))
            {
                Directory.CreateDirectory(_regionDir);
            }
        }

        public bool HasChunk(int3 chunkCoord)
        {
            GetRegionCoords(chunkCoord, out int2 regionCoord, out int localX, out int localZ);
            RegionFile region = GetOrOpenRegion(regionCoord);

            return region.HasChunk(localX, localZ);
        }

        public bool LoadChunk(
            int3 chunkCoord,
            NativeArray<StateId> chunkData,
            NativeArray<byte> lightData)
        {
            GetRegionCoords(chunkCoord, out int2 regionCoord, out int localX, out int localZ);
            RegionFile region = GetOrOpenRegion(regionCoord);

            byte[] serialized = region.LoadChunk(localX, localZ);

            if (serialized == null)
            {
                return false;
            }

            return ChunkSerializer.Deserialize(serialized, chunkData, lightData);
        }

        public void SaveChunk(
            int3 chunkCoord,
            NativeArray<StateId> chunkData,
            NativeArray<byte> lightData)
        {
            GetRegionCoords(chunkCoord, out int2 regionCoord, out int localX, out int localZ);
            RegionFile region = GetOrOpenRegion(regionCoord);

            byte[] serialized = ChunkSerializer.Serialize(chunkData, lightData);
            region.SaveChunk(localX, localZ, serialized);
        }

        public void FlushAll()
        {
            foreach (KeyValuePair<int2, RegionFile> kvp in _regionFiles)
            {
                kvp.Value.Flush();
            }
        }

        public void SaveMetadata(long seed, string contentHash)
        {
            WorldMetadata meta = new WorldMetadata();
            meta.Seed = seed;
            meta.ContentHash = contentHash;
            meta.Save(Path.Combine(_worldDir, "world.json"));
        }

        public WorldMetadata LoadMetadata()
        {
            return WorldMetadata.Load(Path.Combine(_worldDir, "world.json"));
        }

        private RegionFile GetOrOpenRegion(int2 regionCoord)
        {
            if (!_regionFiles.TryGetValue(regionCoord, out RegionFile region))
            {
                string fileName = $"r.{regionCoord.x}.{regionCoord.y}.lfrg";
                string filePath = Path.Combine(_regionDir, fileName);
                region = new RegionFile(filePath);
                _regionFiles[regionCoord] = region;
            }

            return region;
        }

        private static void GetRegionCoords(int3 chunkCoord, out int2 regionCoord, out int localX, out int localZ)
        {
            regionCoord = new int2(
                FloorDiv(chunkCoord.x, RegionFile.RegionSize),
                FloorDiv(chunkCoord.z, RegionFile.RegionSize));

            localX = ((chunkCoord.x % RegionFile.RegionSize) + RegionFile.RegionSize) % RegionFile.RegionSize;
            localZ = ((chunkCoord.z % RegionFile.RegionSize) + RegionFile.RegionSize) % RegionFile.RegionSize;
        }

        private static int FloorDiv(int a, int b)
        {
            return a >= 0 ? a / b : (a - b + 1) / b;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                foreach (KeyValuePair<int2, RegionFile> kvp in _regionFiles)
                {
                    kvp.Value.Dispose();
                }

                _regionFiles.Clear();
            }
        }
    }
}
