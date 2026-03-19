using System;
using System.Collections.Generic;

using Unity.Mathematics;

namespace Lithforge.Network.Client
{
    /// <summary>
    ///     Tracks whether enough chunks within the spawn-ready volume have been loaded
    ///     by the client. Uses a poll-based design: a <see cref="System.Func{int3,}(int3)" />
    ///     delegate is called to check if each required chunk is available. This works
    ///     identically for SP/Host (chunks arrive via LocalChunkStreamingStrategy)
    ///     and remote clients (chunks arrive via ChunkData messages) because both
    ///     ultimately populate the same ChunkManager.
    ///     <para>
    ///         Fires <see cref="OnReadinessAchieved" /> exactly once when all required
    ///         chunks become available. Wire this callback to send
    ///         <see cref="Messages.ClientReadyMessage" /> to the server.
    ///     </para>
    /// </summary>
    public sealed class ClientReadinessTracker
    {
        private readonly Func<int3, bool> _isChunkAvailable;

        private readonly HashSet<int3> _requiredChunks = new();

        private bool _configured;

        /// <summary>
        ///     Fired exactly once when all required chunks become available.
        ///     This fires during a <see cref="GetSnapshot" /> call (which runs on the
        ///     main thread via LoadingScreen polling).
        /// </summary>
        public Action OnReadinessAchieved;

        /// <summary>
        ///     Creates a new tracker with the given chunk availability delegate.
        ///     The delegate should return true when the chunk at the given coordinate
        ///     has been loaded (e.g. ChunkManager.GetChunk(coord)?.State >= Generated).
        /// </summary>
        public ClientReadinessTracker(Func<int3, bool> isChunkAvailable)
        {
            _isChunkAvailable = isChunkAvailable;
        }

        /// <summary>True when all required chunks are available.</summary>
        public bool IsComplete { get; private set; }

        /// <summary>
        ///     Configures the spawn volume. Must be called once before
        ///     <see cref="GetSnapshot" /> returns meaningful results.
        ///     Builds the set of required chunk coordinates from the spawn chunk,
        ///     radius, and Y range.
        /// </summary>
        public void Configure(int3 spawnChunk, int radius, int yMin, int yMax)
        {
            _requiredChunks.Clear();
            IsComplete = false;
            _configured = true;

            for (int x = -radius; x <= radius; x++)
            {
                for (int z = -radius; z <= radius; z++)
                {
                    for (int y = yMin; y <= yMax; y++)
                    {
                        _requiredChunks.Add(new int3(spawnChunk.x + x, y, spawnChunk.z + z));
                    }
                }
            }
        }

        /// <summary>
        ///     Returns a readiness snapshot by polling the availability delegate for
        ///     each required chunk. If the required set becomes fully available,
        ///     fires <see cref="OnReadinessAchieved" /> exactly once.
        ///     Called every frame by the loading screen progress source.
        /// </summary>
        public SpawnReadinessSnapshot GetSnapshot()
        {
            if (!_configured)
            {
                return new SpawnReadinessSnapshot();
            }

            if (IsComplete)
            {
                return new SpawnReadinessSnapshot
                {
                    TotalChunks = _requiredChunks.Count, ReadyChunks = _requiredChunks.Count, IsComplete = true,
                };
            }

            int ready = 0;

            foreach (int3 coord in _requiredChunks)
            {
                if (_isChunkAvailable(coord))
                {
                    ready++;
                }
            }

            if (ready >= _requiredChunks.Count)
            {
                IsComplete = true;
                OnReadinessAchieved?.Invoke();
            }

            return new SpawnReadinessSnapshot
            {
                TotalChunks = _requiredChunks.Count, ReadyChunks = ready, IsComplete = IsComplete,
            };
        }
    }
}
