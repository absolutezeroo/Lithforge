using System;
using System.Collections.Generic;
using System.IO;
using Lithforge.Core.Logging;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.BlockEntity;
using Lithforge.Voxel.Chunk;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Profiling;

namespace Lithforge.Voxel.Storage
{
    public sealed class WorldStorage : IDisposable
    {
        private readonly string _worldDir;
        private readonly string _regionDir;
        private readonly Dictionary<int3, RegionFile> _regionFiles = new Dictionary<int3, RegionFile>();
        private readonly ILogger _logger;
        private bool _disposed;

        public string WorldDir
        {
            get { return _worldDir; }
        }

        public WorldStorage(string worldDir, ILogger logger = null)
        {
            _worldDir = worldDir;
            _regionDir = Path.Combine(worldDir, "region");
            _logger = logger;

            if (!Directory.Exists(_regionDir))
            {
                Directory.CreateDirectory(_regionDir);
            }
        }

        public bool HasChunk(int3 chunkCoord)
        {
            try
            {
                GetRegionCoords(chunkCoord, out int3 regionCoord, out int localX, out int localZ);
                RegionFile region = GetOrOpenRegion(regionCoord);

                return region.HasChunk(localX, localZ);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"[WorldStorage] HasChunk failed for {chunkCoord}: {ex.Message}");

                return false;
            }
        }

        public bool LoadChunk(int3 chunkCoord,
            NativeArray<StateId> chunkData,
            NativeArray<byte> lightData,
            out Dictionary<int, IBlockEntity> blockEntities,
            BlockEntityRegistry blockEntityRegistry = null)
        {
            blockEntities = null;

            Profiler.BeginSample("WS.LoadChunk");
            try
            {
                GetRegionCoords(chunkCoord, out int3 regionCoord, out int localX, out int localZ);
                RegionFile region = GetOrOpenRegion(regionCoord);

                byte[] serialized = region.LoadChunk(localX, localZ);

                if (serialized == null)
                {
                    return false;
                }

                return ChunkSerializer.Deserialize(serialized, chunkData, lightData, out blockEntities, blockEntityRegistry);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"[WorldStorage] LoadChunk failed for {chunkCoord}: {ex.Message}");

                return false;
            }
            finally
            {
                Profiler.EndSample();
            }
        }

        /// <summary>
        /// Backward-compatible overload without block entity support.
        /// </summary>
        public bool LoadChunk(
            int3 chunkCoord,
            NativeArray<StateId> chunkData,
            NativeArray<byte> lightData)
        {
            return LoadChunk(chunkCoord, chunkData, lightData, out Dictionary<int, IBlockEntity> _, null);
        }

        public void SaveChunk(
            int3 chunkCoord,
            NativeArray<StateId> chunkData,
            NativeArray<byte> lightData,
            Dictionary<int, IBlockEntity> blockEntities = null)
        {
            Profiler.BeginSample("WS.SaveChunk");
            try
            {
                GetRegionCoords(chunkCoord, out int3 regionCoord, out int localX, out int localZ);
                RegionFile region = GetOrOpenRegion(regionCoord);

                byte[] serialized = ChunkSerializer.Serialize(chunkData, lightData, blockEntities);
                region.SaveChunk(localX, localZ, serialized);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"[WorldStorage] SaveChunk failed for {chunkCoord}: {ex.Message}");
            }
            finally
            {
                Profiler.EndSample();
            }
        }

        public void FlushAll(bool onlyDirty = false)
        {
            foreach (KeyValuePair<int3, RegionFile> kvp in _regionFiles)
            {
                if (onlyDirty && !kvp.Value.IsDirty)
                {
                    continue;
                }

                try
                {
                    kvp.Value.Flush();
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"[WorldStorage] Flush failed for region {kvp.Key}: {ex.Message}");
                }
            }
        }

        public void SaveMetadata(long seed, string contentHash)
        {
            WorldMetadata meta = new WorldMetadata();
            meta.Seed = seed;
            meta.ContentHash = contentHash;
            meta.Save(Path.Combine(_worldDir, "world.json"));
        }

        public void SaveMetadataFull(WorldMetadata metadata)
        {
            if (metadata == null)
            {
                return;
            }

            metadata.Save(Path.Combine(_worldDir, "world.json"));
        }

        public WorldMetadata LoadMetadata()
        {
            return WorldMetadata.Load(Path.Combine(_worldDir, "world.json"));
        }

        private RegionFile GetOrOpenRegion(int3 regionCoord)
        {
            if (!_regionFiles.TryGetValue(regionCoord, out RegionFile region))
            {
                string fileName = $"r.{regionCoord.x}.{regionCoord.y}.{regionCoord.z}.lfrg";
                string filePath = Path.Combine(_regionDir, fileName);
                region = new RegionFile(filePath);
                _regionFiles[regionCoord] = region;
            }

            return region;
        }

        private static void GetRegionCoords(int3 chunkCoord, out int3 regionCoord, out int localX, out int localZ)
        {
            regionCoord = new int3(
                FloorDiv(chunkCoord.x, RegionFile.RegionSize),
                chunkCoord.y,
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

                foreach (KeyValuePair<int3, RegionFile> kvp in _regionFiles)
                {
                    kvp.Value.Dispose();
                }

                _regionFiles.Clear();
            }
        }
    }
}
