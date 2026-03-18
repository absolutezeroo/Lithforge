using Lithforge.Network.Server;

using Unity.Mathematics;

namespace Lithforge.Runtime.Spawn
{
    /// <summary>
    ///     Polls <see cref="IServerChunkProvider" /> for spawn volume readiness,
    ///     producing <see cref="SpawnProgress" /> snapshots for the loading screen.
    ///     Used in SP/Host mode where the server and client share a process.
    ///     The spawn chunk is updated when the server accepts the local peer.
    /// </summary>
    public sealed class SpawnLoadingTracker
    {
        private readonly IServerChunkProvider _provider;
        private readonly int _readyRadius;
        private readonly int _yMax;
        private readonly int _yMin;
        private int3 _spawnChunk;

        public SpawnLoadingTracker(
            IServerChunkProvider provider,
            int3 spawnChunk,
            int readyRadius,
            int yMin,
            int yMax)
        {
            _provider = provider;
            _spawnChunk = spawnChunk;
            _readyRadius = readyRadius;
            _yMin = yMin;
            _yMax = yMax;
        }

        /// <summary>
        ///     Updates the spawn chunk coordinate. Called when the server computes
        ///     the spawn position for the local peer.
        /// </summary>
        public void UpdateSpawnChunk(int3 spawnChunk)
        {
            _spawnChunk = spawnChunk;
        }

        /// <summary>
        ///     Returns a snapshot of spawn loading progress.
        ///     Returns <see cref="SpawnState.Checking" /> while chunks are loading,
        ///     <see cref="SpawnState.Done" /> when all chunks in the spawn volume
        ///     are ready (meshed or all-air).
        /// </summary>
        public SpawnProgress GetProgress()
        {
            SpawnReadinessSnapshot snapshot = _provider.GetSpawnReadiness(
                _spawnChunk, _readyRadius, _yMin, _yMax, requireMeshed: true);

            return new SpawnProgress
            {
                Phase = snapshot.IsComplete ? SpawnState.Done : SpawnState.Checking,
                TotalChunks = snapshot.TotalChunks,
                ReadyChunks = snapshot.ReadyChunks,
            };
        }
    }
}
