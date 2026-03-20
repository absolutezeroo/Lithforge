using System;

using Lithforge.Item;
using Lithforge.Voxel.Storage;

using UnityEngine;

namespace Lithforge.Runtime.World
{
    /// <summary>Periodically saves world metadata and flushes dirty chunks to disk.</summary>
    public sealed class AutoSaveManager
    {
        /// <summary>World storage for flushing region files.</summary>
        private readonly WorldStorage _worldStorage;

        /// <summary>World metadata containing player state and world properties.</summary>
        private readonly WorldMetadata _worldMetadata;

        /// <summary>Player transform for capturing position in metadata.</summary>
        private readonly Transform _playerTransform;

        /// <summary>Main camera for capturing rotation in metadata.</summary>
        private readonly Camera _mainCamera;

        /// <summary>Delegate returning the current time of day for metadata.</summary>
        private readonly Func<float> _getTimeOfDay;

        /// <summary>Player inventory for serializing slot contents.</summary>
        private readonly Inventory _inventory;

        /// <summary>Optional player data store for per-player save files.</summary>
        private PlayerDataStore _playerDataStore;

        /// <summary>Optional async chunk saver flushed before region file writes.</summary>
        private AsyncChunkSaver _asyncSaver;

        /// <summary>Realtime timestamp of the last chunk flush, or -1 if not yet run.</summary>
        private float _lastChunkFlushTime = -1f;

        /// <summary>Realtime timestamp of the last metadata flush, or -1 if not yet run.</summary>
        private float _lastMetaFlushTime = -1f;

        /// <summary>Seconds between chunk data flushes to disk.</summary>
        private const float ChunkFlushInterval = 60f;

        /// <summary>Seconds between metadata saves to disk.</summary>
        private const float MetaFlushInterval = 30f;

        /// <summary>Creates the auto-save manager with all required references.</summary>
        public AutoSaveManager(
            WorldStorage worldStorage,
            WorldMetadata worldMetadata,
            Transform playerTransform,
            Camera mainCamera,
            Func<float> getTimeOfDay,
            Inventory inventory)
        {
            _worldStorage = worldStorage;
            _worldMetadata = worldMetadata;
            _playerTransform = playerTransform;
            _mainCamera = mainCamera;
            _getTimeOfDay = getTimeOfDay;
            _inventory = inventory;
        }

        /// <summary>
        /// Sets the AsyncChunkSaver so that pending async writes are flushed
        /// before region files are written to disk.
        /// </summary>
        public void SetAsyncSaver(AsyncChunkSaver asyncSaver)
        {
            _asyncSaver = asyncSaver;
        }

        /// <summary>Injects the player data store for per-player save files.</summary>
        public void SetPlayerDataStore(PlayerDataStore playerDataStore)
        {
            _playerDataStore = playerDataStore;
        }

        /// <summary>Checks timers and flushes metadata and/or chunks when intervals elapse.</summary>
        public void Tick(float realtimeSinceStartup)
        {
            if (_worldStorage == null)
            {
                return;
            }

            // Initialize timers on first tick so saves don't fire immediately
            if (_lastMetaFlushTime < 0f)
            {
                _lastMetaFlushTime = realtimeSinceStartup;
            }

            if (_lastChunkFlushTime < 0f)
            {
                _lastChunkFlushTime = realtimeSinceStartup;
            }

            if (realtimeSinceStartup >= _lastMetaFlushTime + MetaFlushInterval)
            {
                SaveMetadata();
                _lastMetaFlushTime = realtimeSinceStartup;
            }

            if (realtimeSinceStartup >= _lastChunkFlushTime + ChunkFlushInterval)
            {
                if (_asyncSaver != null)
                {
                    _asyncSaver.Flush();
                }

                _worldStorage.FlushAll(true);
                _lastChunkFlushTime = realtimeSinceStartup;
            }
        }

        /// <summary>Immediately saves metadata and flushes all region files.</summary>
        public void ForceSave()
        {
            SaveMetadata();
            _worldStorage.FlushAll();
        }

        /// <summary>
        /// Saves metadata only, without flushing region files.
        /// Used during shutdown where SaveAllChunks + FlushAll happens separately.
        /// </summary>
        public void SaveMetadataOnly()
        {
            SaveMetadata();
        }

        /// <summary>Captures player state and persists world metadata to disk.</summary>
        private void SaveMetadata()
        {
            if (_worldStorage == null)
            {
                return;
            }

            WorldPlayerState captured = PlayerStateSerializer.Capture(
                _playerTransform,
                _mainCamera,
                _getTimeOfDay(),
                _inventory);

            if (_playerDataStore is not null)
            {
                _playerDataStore.Save("local", captured);
            }

            _worldStorage.SaveMetadataFull(_worldMetadata);
        }
    }
}
