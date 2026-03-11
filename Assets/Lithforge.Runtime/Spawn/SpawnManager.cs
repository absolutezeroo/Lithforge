using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Unity.Mathematics;
using UnityEngine;

namespace Lithforge.Runtime.Spawn
{
    /// <summary>
    /// Coordinates the spawn loading process: waits for chunks to reach Ready,
    /// finds a safe spawn Y, and teleports the player.
    /// Called each frame by GameLoop.Update() until IsComplete is true.
    /// </summary>
    public sealed class SpawnManager
    {
        private readonly ChunkManager _chunkManager;
        private readonly NativeStateRegistry _nativeStateRegistry;
        private readonly Transform _playerTransform;
        private readonly int _spawnRadius;
        private readonly int _yMin;
        private readonly int _yMax;
        private readonly int _fallbackY;

        private int3 _spawnChunkCoord;
        private SpawnProgress _progress;

        /// <summary>
        /// True once the spawn process is fully complete and the player has been placed.
        /// </summary>
        public bool IsComplete
        {
            get { return _progress.Phase == SpawnState.Done; }
        }

        public SpawnManager(
            ChunkManager chunkManager,
            NativeStateRegistry nativeStateRegistry,
            Transform playerTransform,
            int spawnRadius,
            int yMin = -1,
            int yMax = 3,
            int fallbackY = 65)
        {
            _chunkManager = chunkManager;
            _nativeStateRegistry = nativeStateRegistry;
            _playerTransform = playerTransform;
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
        /// Returns a snapshot of the current spawn progress.
        /// </summary>
        public SpawnProgress GetProgress()
        {
            return _progress;
        }

        /// <summary>
        /// Advances the spawn state machine by one step.
        /// Must be called from the main thread each frame while !IsComplete.
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
                        int3 coord = new int3(
                            _spawnChunkCoord.x + x,
                            _spawnChunkCoord.y + y,
                            _spawnChunkCoord.z + z);
                        ManagedChunk chunk = _chunkManager.GetChunk(coord);

                        if (chunk != null && chunk.State == ChunkState.Ready)
                        {
                            readyCount++;
                        }
                    }
                }
            }

            _progress.ReadyChunks = readyCount;

            if (readyCount >= _progress.TotalChunks)
            {
                _progress.Phase = SpawnState.FindingY;
            }
        }

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

        private void TickTeleporting()
        {
            if (_playerTransform != null)
            {
                _playerTransform.position = new Vector3(
                    _progress.SpawnX + 0.5f,
                    _progress.SpawnY,
                    _progress.SpawnZ + 0.5f);

                UnityEngine.Debug.Log(
                    $"[SpawnManager] Spawn complete at ({_progress.SpawnX}, {_progress.SpawnY}, {_progress.SpawnZ})");
            }

            _progress.Phase = SpawnState.Done;
            _progress.ReadyChunks = _progress.TotalChunks;
        }

        /// <summary>
        /// Scans downward from the top of the spawn volume to find the highest air block
        /// above a solid block. Restricts scan to the confirmed-ready chunk range.
        /// Returns the Y coordinate for player feet placement.
        /// </summary>
        private int FindSafeSpawnY(int worldX, int worldZ)
        {
            // Restrict scan to chunks within the spawn radius (all confirmed Ready)
            int maxY = (_spawnChunkCoord.y + _yMax + 1) * ChunkConstants.Size - 1;
            int minY = (_spawnChunkCoord.y + _yMin) * ChunkConstants.Size;

            for (int y = maxY; y >= minY; y--)
            {
                int3 blockCoord = new int3(worldX, y, worldZ);
                StateId stateId = _chunkManager.GetBlock(blockCoord);
                BlockStateCompact state = _nativeStateRegistry.States[stateId.Value];

                if (state.CollisionShape != 0)
                {
                    return y + 1;
                }
            }

            // No solid block found — use fallback Y
            return _fallbackY;
        }
    }
}
