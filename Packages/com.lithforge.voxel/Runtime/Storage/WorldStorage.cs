using System;
using System.Collections.Generic;
using System.IO;

using Lithforge.Core.Logging;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.BlockEntity;

using Unity.Collections;
using Unity.Mathematics;

namespace Lithforge.Voxel.Storage
{
    /// <summary>
    ///     High-level storage layer that reads and writes chunk data via region files.
    ///     Each region file covers a 32x32 XZ area. Region files are opened lazily
    ///     and cached for the session lifetime. Thread-safe via _regionFilesLock.
    /// </summary>
    public sealed class WorldStorage : IDisposable
    {
        /// <summary>Reusable list for collecting dirty regions during FlushAll (avoids per-call allocation).</summary>
        private readonly List<RegionFile> _flushCache = new();

        /// <summary>Logger for error reporting on load/save failures.</summary>
        private readonly ILogger _logger;

        /// <summary>Full filesystem path to the "region" subdirectory within the world directory.</summary>
        private readonly string _regionDir;

        /// <summary>Cache of open region files, keyed by region coordinate (chunk coord / 32).</summary>
        private readonly Dictionary<int3, RegionFile> _regionFiles = new();

        /// <summary>Lock protecting _regionFiles for thread-safe region access from AsyncChunkSaver.</summary>
        private readonly object _regionFilesLock = new();

        /// <summary>Whether this storage instance has been disposed.</summary>
        private bool _disposed;

        /// <summary>Opens or creates the world storage at the given world directory path.</summary>
        public WorldStorage(string worldDir, ILogger logger = null)
        {
            WorldDir = worldDir;
            _regionDir = Path.Combine(worldDir, "region");
            _logger = logger;

            if (!Directory.Exists(_regionDir))
            {
                Directory.CreateDirectory(_regionDir);
            }
        }

        /// <summary>Full filesystem path to the world directory.</summary>
        public string WorldDir { get; }

        /// <summary>Disposes all open region files and releases their resources.</summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                lock (_regionFilesLock)
                {
                    foreach (KeyValuePair<int3, RegionFile> kvp in _regionFiles)
                    {
                        kvp.Value.Dispose();
                    }

                    _regionFiles.Clear();
                }
            }
        }

        /// <summary>Returns true if the region file for this chunk has data for the given coordinate.</summary>
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

        /// <summary>
        ///     Loads and deserializes a chunk from the region file into pre-allocated NativeArrays.
        ///     Returns false if the chunk does not exist or deserialization fails.
        /// </summary>
        public bool LoadChunk(int3 chunkCoord,
            NativeArray<StateId> chunkData,
            NativeArray<byte> lightData,
            out Dictionary<int, IBlockEntity> blockEntities,
            BlockEntityRegistry blockEntityRegistry = null)
        {
            blockEntities = null;

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
        }

        /// <summary>
        ///     Backward-compatible overload without block entity support.
        /// </summary>
        public bool LoadChunk(
            int3 chunkCoord,
            NativeArray<StateId> chunkData,
            NativeArray<byte> lightData)
        {
            return LoadChunk(chunkCoord, chunkData, lightData, out Dictionary<int, IBlockEntity> _);
        }

        /// <summary>
        ///     Serializes and saves a chunk to the region file cache.
        ///     Data is not written to disk until FlushAll or region Dispose.
        /// </summary>
        public void SaveChunk(
            int3 chunkCoord,
            NativeArray<StateId> chunkData,
            NativeArray<byte> lightData,
            Dictionary<int, IBlockEntity> blockEntities = null)
        {
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
        }

        /// <summary>
        ///     Saves pre-serialized chunk data to the region file cache.
        ///     Used by AsyncChunkSaver worker thread — thread-safe via region file lock.
        /// </summary>
        public void SaveChunkRaw(int3 chunkCoord, byte[] serializedData)
        {
            try
            {
                GetRegionCoords(chunkCoord, out int3 regionCoord, out int localX, out int localZ);
                RegionFile region = GetOrOpenRegion(regionCoord);
                region.SaveChunk(localX, localZ, serializedData);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"[WorldStorage] SaveChunkRaw failed for {chunkCoord}: {ex.Message}");
            }
        }

        /// <summary>
        ///     Fills the provided list with region files that have unsaved data.
        ///     Clears the list before filling. Caller uses fill pattern.
        /// </summary>
        public void CollectDirtyRegions(List<RegionFile> result)
        {
            result.Clear();

            lock (_regionFilesLock)
            {
                foreach (KeyValuePair<int3, RegionFile> kvp in _regionFiles)
                {
                    if (kvp.Value.IsDirty)
                    {
                        result.Add(kvp.Value);
                    }
                }
            }
        }

        /// <summary>
        ///     Flushes a single region file to disk with error handling.
        ///     Used by incremental save coroutine for per-region progress reporting.
        /// </summary>
        public void FlushRegion(RegionFile region)
        {
            try
            {
                region.Flush();
            }
            catch (Exception ex)
            {
                _logger?.LogError($"[WorldStorage] FlushRegion failed: {ex.Message}");
            }
        }

        /// <summary>
        ///     Flushes all (or only dirty) region files to disk via atomic write.
        ///     Called at shutdown and during incremental save.
        /// </summary>
        public void FlushAll(bool onlyDirty = false)
        {
            _flushCache.Clear();

            lock (_regionFilesLock)
            {
                foreach (KeyValuePair<int3, RegionFile> kvp in _regionFiles)
                {
                    if (onlyDirty && !kvp.Value.IsDirty)
                    {
                        continue;
                    }

                    _flushCache.Add(kvp.Value);
                }
            }

            for (int i = 0; i < _flushCache.Count; i++)
            {
                try
                {
                    _flushCache[i].Flush();
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"[WorldStorage] Flush failed: {ex.Message}");
                }
            }

            _flushCache.Clear();
        }

        /// <summary>
        ///     Incrementally flushes dirty region files by appending new sectors rather than
        ///     rewriting entire files. Faster than <see cref="FlushAll" /> but accumulates dead
        ///     sectors. Periodic <see cref="FlushAll" /> compaction is recommended.
        /// </summary>
        public void FlushAllIncremental()
        {
            _flushCache.Clear();

            lock (_regionFilesLock)
            {
                foreach (KeyValuePair<int3, RegionFile> kvp in _regionFiles)
                {
                    if (kvp.Value.IsDirty)
                    {
                        _flushCache.Add(kvp.Value);
                    }
                }
            }

            for (int i = 0; i < _flushCache.Count; i++)
            {
                try
                {
                    _flushCache[i].FlushIncremental();
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"[WorldStorage] FlushIncremental failed: {ex.Message}");
                }
            }

            _flushCache.Clear();
        }

        /// <summary>Saves a minimal world.json with seed and content hash.</summary>
        public void SaveMetadata(long seed, string contentHash)
        {
            WorldMetadata meta = new()
            {
                Seed = seed, ContentHash = contentHash,
            };
            meta.Save(Path.Combine(WorldDir, "world.json"));
        }

        /// <summary>Saves a complete WorldMetadata to world.json (player state, timestamps, etc.).</summary>
        public void SaveMetadataFull(WorldMetadata metadata)
        {
            if (metadata == null)
            {
                return;
            }

            metadata.Save(Path.Combine(WorldDir, "world.json"));
        }

        /// <summary>Loads world.json metadata, or returns null if the file does not exist.</summary>
        public WorldMetadata LoadMetadata()
        {
            return WorldMetadata.Load(Path.Combine(WorldDir, "world.json"));
        }

        /// <summary>Returns a cached RegionFile for the given region coordinate, opening it if needed.</summary>
        private RegionFile GetOrOpenRegion(int3 regionCoord)
        {
            lock (_regionFilesLock)
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
        }

        /// <summary>Converts a chunk coordinate to region coordinate and local XZ indices within the region.</summary>
        private static void GetRegionCoords(int3 chunkCoord, out int3 regionCoord, out int localX, out int localZ)
        {
            regionCoord = new int3(
                FloorDiv(chunkCoord.x, RegionFile.RegionSize),
                chunkCoord.y,
                FloorDiv(chunkCoord.z, RegionFile.RegionSize));

            localX = (chunkCoord.x % RegionFile.RegionSize + RegionFile.RegionSize) % RegionFile.RegionSize;
            localZ = (chunkCoord.z % RegionFile.RegionSize + RegionFile.RegionSize) % RegionFile.RegionSize;
        }

        /// <summary>Floor division that handles negative dividends correctly.</summary>
        private static int FloorDiv(int a, int b)
        {
            return a >= 0 ? a / b : (a - b + 1) / b;
        }
    }
}
