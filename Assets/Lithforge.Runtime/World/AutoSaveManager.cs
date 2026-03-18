using System;
using Lithforge.Item;
using Lithforge.Voxel.Storage;
using UnityEngine;

namespace Lithforge.Runtime.World
{
    public sealed class AutoSaveManager
    {
        private readonly WorldStorage _worldStorage;
        private readonly WorldMetadata _worldMetadata;
        private readonly Transform _playerTransform;
        private readonly Camera _mainCamera;
        private readonly Func<float> _getTimeOfDay;
        private readonly Inventory _inventory;

        private AsyncChunkSaver _asyncSaver;
        private float _lastChunkFlushTime = -1f;
        private float _lastMetaFlushTime = -1f;

        private const float ChunkFlushInterval = 60f;
        private const float MetaFlushInterval = 30f;

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

        private void SaveMetadata()
        {
            if (_worldStorage == null)
            {
                return;
            }

            _worldMetadata.PlayerState = PlayerStateSerializer.Capture(
                _playerTransform,
                _mainCamera,
                _getTimeOfDay(),
                _inventory);

            _worldStorage.SaveMetadataFull(_worldMetadata);
        }
    }
}
