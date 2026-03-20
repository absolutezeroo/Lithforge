using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Spawn;

using Unity.Mathematics;

using UnityEngine;

using ILogger = Lithforge.Core.Logging.ILogger;

namespace Lithforge.Runtime.Spawn
{
    /// <summary>
    ///     Coordinates the spawn loading process: waits for chunks to reach Ready,
    ///     finds a safe spawn Y, and teleports the player.
    ///     Called each frame by GameLoop.Update() until IsComplete is true.
    /// </summary>
    public sealed class SpawnManager
    {
        /// <summary>Chunk manager for querying chunk readiness and block state.</summary>
        private readonly ChunkManager _chunkManager;

        /// <summary>Fallback Y coordinate if no safe surface is found.</summary>
        private readonly int _fallbackY;

        /// <summary>Logger for spawn diagnostics.</summary>
        private readonly ILogger _logger;

        /// <summary>Native state registry for checking block solidity during safe-spawn search.</summary>
        private readonly NativeStateRegistry _nativeStateRegistry;

        /// <summary>Player transform to position once spawn is resolved.</summary>
        private readonly Transform _playerTransform;

        /// <summary>Chunk coordinate at the center of the spawn volume.</summary>
        private readonly int3 _spawnChunkCoord;

        /// <summary>Radius in chunks around the spawn coordinate that must reach Ready state.</summary>
        private readonly int _spawnRadius;

        /// <summary>Maximum chunk-Y offset (inclusive) above the spawn chunk for readiness checks.</summary>
        private readonly int _yMax;

        /// <summary>Minimum chunk-Y offset (inclusive) below the spawn chunk for readiness checks.</summary>
        private readonly int _yMin;

        /// <summary>Mutable spawn progress snapshot, updated each tick.</summary>
        private SpawnProgress _progress;

        /// <summary>When true, skips FindingY and Teleporting if the saved position is safe.</summary>
        private bool _skipSpawnSearch;

        /// <summary>
        ///     Creates the spawn manager, computes the spawn volume from the player's
        ///     initial position, and clamped radius to render distance.
        /// </summary>
        public SpawnManager(
            ChunkManager chunkManager,
            NativeStateRegistry nativeStateRegistry,
            Transform playerTransform,
            int spawnRadius,
            int yMin = -1,
            int yMax = 3,
            int fallbackY = 65,
            ILogger logger = null)
        {
            _chunkManager = chunkManager;
            _nativeStateRegistry = nativeStateRegistry;
            _playerTransform = playerTransform;
            _logger = logger;
            _yMin = yMin;
            _yMax = yMax;
            _fallbackY = fallbackY;

            // Clamp spawn radius to render distance to prevent deadlock when
            // persisted render distance is smaller than the configured spawn radius
            _spawnRadius = math.min(spawnRadius, chunkManager.RenderDistance);

            // Compute spawn chunk coord from player's initial position
            Vector3 pos = _playerTransform.position;
            _spawnChunkCoord = new int3(
                (int)math.floor(pos.x / ChunkConstants.Size),
                (int)math.floor(pos.y / ChunkConstants.Size),
                (int)math.floor(pos.z / ChunkConstants.Size));

            int diameter = _spawnRadius * 2 + 1;
            int yLevels = _yMax - _yMin + 1;
            _progress.TotalChunks = diameter * diameter * yLevels;
            _progress.Phase = SpawnState.Checking;
        }

        /// <summary>
        ///     True once the spawn process is fully complete and the player has been placed.
        /// </summary>
        public bool IsComplete
        {
            get
            {
                return _progress.Phase == SpawnState.Done;
            }
        }

        /// <summary>
        ///     Marks this spawn as a restore from saved position.
        ///     When Checking completes, the player is already at the correct position,
        ///     so FindingY and Teleporting are skipped — transition straight to Done.
        ///     Must be called before the first Tick().
        /// </summary>
        public void SetSavedPosition()
        {
            _skipSpawnSearch = true;
        }

        /// <summary>
        ///     Returns a snapshot of the current spawn progress.
        /// </summary>
        public SpawnProgress GetProgress()
        {
            return _progress;
        }

        /// <summary>
        ///     Advances the spawn state machine by one step.
        ///     Must be called from the main thread each frame while !IsComplete.
        /// </summary>
        public void Tick()
        {
            if (_progress.Phase == SpawnState.Done)
            {
                return;
            }

            if (_progress.Phase == SpawnState.Checking)
            {
                TickChecking();
            }
            else if (_progress.Phase == SpawnState.FindingY)
            {
                TickFindingY();
            }
            else if (_progress.Phase == SpawnState.Teleporting)
            {
                TickTeleporting();
            }
        }

        /// <summary>Counts Ready chunks in the spawn volume; transitions to FindingY when all are ready.</summary>
        private void TickChecking()
        {
            int readyCount = 0;
            int r = _spawnRadius;

            for (int x = -r; x <= r; x++)
            {
                for (int z = -r; z <= r; z++)
                {
                    for (int y = _yMin; y <= _yMax; y++)
                    {
                        int3 coord = new(
                            _spawnChunkCoord.x + x,
                            _spawnChunkCoord.y + y,
                            _spawnChunkCoord.z + z);
                        ManagedChunk chunk = _chunkManager.GetChunk(coord);

                        if (chunk is
                            {
                                State: ChunkState.Ready,
                            })
                        {
                            readyCount++;
                        }
                    }
                }
            }

            _progress.ReadyChunks = readyCount;

            if (readyCount >= _progress.TotalChunks)
            {
                if (_skipSpawnSearch && IsRestoredPositionSafe())
                {
                    // Player already at saved position and not stuck in blocks
                    _progress.Phase = SpawnState.Done;
                    _progress.ReadyChunks = _progress.TotalChunks;
                }
                else
                {
                    if (_skipSpawnSearch)
                    {
#if LITHFORGE_DEBUG
                        _logger?.LogWarning(
                            "[SpawnManager] Saved position is inside solid blocks, falling back to FindingY.");
#endif
                    }

                    _progress.Phase = SpawnState.FindingY;
                }
            }
        }

        /// <summary>Finds a safe Y coordinate at the center of the spawn volume and transitions to Teleporting.</summary>
        private void TickFindingY()
        {
            int spawnX = _spawnChunkCoord.x * ChunkConstants.Size + ChunkConstants.Size / 2;
            int spawnZ = _spawnChunkCoord.z * ChunkConstants.Size + ChunkConstants.Size / 2;
            int safeY = FindSafeSpawnY(spawnX, spawnZ);

            _progress.SpawnX = spawnX;
            _progress.SpawnY = safeY;
            _progress.SpawnZ = spawnZ;
            _progress.Phase = SpawnState.Teleporting;
        }

        /// <summary>Teleports the player to the resolved spawn position and transitions to Done.</summary>
        private void TickTeleporting()
        {
            if (_playerTransform != null)
            {
                _playerTransform.position = new Vector3(
                    _progress.SpawnX + 0.5f,
                    _progress.SpawnY,
                    _progress.SpawnZ + 0.5f);

#if LITHFORGE_DEBUG
                _logger?.LogInfo(
                    $"[SpawnManager] Spawn complete at ({_progress.SpawnX}, {_progress.SpawnY}, {_progress.SpawnZ})");
#endif
            }

            _progress.Phase = SpawnState.Done;
            _progress.ReadyChunks = _progress.TotalChunks;
        }

        /// <summary>Checks whether the player's current saved position has non-solid blocks at feet and head height.</summary>
        private bool IsRestoredPositionSafe()
        {
            Vector3 pos = _playerTransform.position;
            int3 feetBlock = new(
                (int)math.floor(pos.x),
                (int)math.floor(pos.y),
                (int)math.floor(pos.z));

            return SpawnUtility.IsPositionSafe(
                _chunkManager.GetBlock, _nativeStateRegistry, feetBlock);
        }

        /// <summary>Scans downward through the spawn volume to find a safe Y coordinate for the given XZ column.</summary>
        private int FindSafeSpawnY(int worldX, int worldZ)
        {
            return SpawnUtility.FindSafeSpawnY(
                _chunkManager.GetBlock,
                _nativeStateRegistry,
                worldX, worldZ,
                _spawnChunkCoord.y + _yMin,
                _spawnChunkCoord.y + _yMax,
                _fallbackY);
        }
    }
}
