using System.Collections.Generic;

using Lithforge.Core.Logging;
using Lithforge.Network.Connection;
using Lithforge.Voxel.Chunk;

using Unity.Mathematics;

namespace Lithforge.Network.Server
{
    /// <summary>
    ///     Manages per-player chunk streaming: rate limiting, spiral priority ordering,
    ///     look-direction bias, Y-level priority, skip-air optimization,
    ///     boundary-crossing unloads, and Loading→Playing gating.
    ///     Stateless — all per-player state lives in <see cref="PlayerInterestState" />.
    /// </summary>
    public sealed class ChunkStreamingManager
    {
        /// <summary>Chunks per tick while queue is non-empty and player is stationary.</summary>
        private const int TrickleRate = 1;

        /// <summary>Chunks per tick while the player is moving through new chunks.</summary>
        private const int MovingRate = 2;

        /// <summary>Chunks per tick during initial Loading phase.</summary>
        private const int InitialLoadRate = 4;

        /// <summary>Weight for look-direction bias when scoring chunk priority.</summary>
        private const float LookBiasWeight = 0.4f;

        /// <summary>
        ///     Weight for Y-level priority penalty. Chunks above the player's Y level
        ///     get a score penalty proportional to their height above the player,
        ///     deprioritizing sky chunks in favor of ground-level terrain.
        /// </summary>
        private const float YPriorityWeight = 0.5f;

        private readonly List<int3> _candidateChunks = new();

        private readonly List<float> _candidateScores = new();

        private readonly IServerChunkProvider _chunkProvider;

        private readonly ILogger _logger;

        private readonly HashSet<int3> _newInterestSet = new();

        private readonly List<int3> _unloadCandidates = new();

        public ChunkStreamingManager(
            int yLoadMin,
            int yLoadMax,
            int readyRadius,
            IServerChunkProvider chunkProvider,
            ILogger logger)
        {
            YLoadMin = yLoadMin;
            YLoadMax = yLoadMax;
            ReadyRadius = readyRadius;
            _chunkProvider = chunkProvider;
            _logger = logger;
        }

        public int YLoadMin { get; }

        public int YLoadMax { get; }

        /// <summary>The spawn-ready radius used for readiness gating.</summary>
        public int ReadyRadius { get; }

        /// <summary>
        ///     Processes chunk streaming for a single peer. Called each tick from ServerGameLoop Phase 5.
        ///     Handles streaming queue rebuild on chunk boundary crossing, chunk unloads, and
        ///     rate-limited chunk data sends via the provided streaming strategy.
        ///     All-air chunks are marked loaded immediately without invoking the strategy.
        /// </summary>
        public void ProcessForPeer(
            PeerInfo peer,
            IChunkStreamingStrategy strategy,
            uint currentTick)
        {
            PlayerInterestState interest = peer.InterestState;

            if (interest == null)
            {
                return;
            }

            // Detect chunk boundary crossing (capture before updating PreviousChunk)
            bool crossedBoundary = !interest.CurrentChunk.Equals(interest.PreviousChunk);

            if (crossedBoundary)
            {
                RebuildStreamingQueue(interest);
                ProcessUnloads(peer, interest, strategy);
                interest.PreviousChunk = interest.CurrentChunk;
            }

            // Adaptive rate: initial load (4) → moving (2) → trickle (1) → idle (0).
            // ACK window cap: pause entirely if too many chunks are in-flight.
            int rate;

            if (interest.UnackedChunks >= interest.MaxInFlightChunks)
            {
                // Flow control: client hasn't ACK'd enough chunks yet
                return;
            }

            if (interest.IsInitialLoad)
            {
                rate = InitialLoadRate;
            }
            else if (crossedBoundary)
            {
                // Player crossed a chunk boundary this tick — moving
                rate = MovingRate;
            }
            else if (interest.StreamingQueueIndex < interest.StreamingQueue.Count)
            {
                // Stationary but queue has pending items — trickle
                rate = TrickleRate;
            }
            else
            {
                // Stationary, all visible chunks loaded — idle
                return;
            }

            // Clamp rate to remaining ACK window
            int windowRemaining = interest.MaxInFlightChunks - interest.UnackedChunks;
            rate = rate < windowRemaining ? rate : windowRemaining;

            int maxChecks = rate * 64;
            int sent = 0;
            int checked_ = 0;
            bool hasSkipped = false;

            while (sent < rate && checked_ < maxChecks
                                && interest.StreamingQueueIndex < interest.StreamingQueue.Count)
            {
                int3 coord = interest.StreamingQueue[interest.StreamingQueueIndex];

                // Skip if already loaded (can happen if queue was rebuilt while some chunks were in-flight)
                if (interest.LoadedChunks.Contains(coord))
                {
                    interest.StreamingQueueIndex++;
                    continue;
                }

                checked_++;

                // Skip-air fast path: all-air chunks require no mesh and no network data.
                // Mark as loaded immediately without invoking the strategy.
                if (_chunkProvider.IsChunkAllAir(coord))
                {
                    interest.LoadedChunks.Add(coord);
                    interest.StreamingQueueIndex++;
                    sent++;
                    continue;
                }

                bool delivered = strategy.StreamChunk(peer, coord);

                if (!delivered)
                {
                    // Chunk not ready — skip for now, but flag for retry
                    hasSkipped = true;
                    interest.StreamingQueueIndex++;
                    continue;
                }

                interest.LoadedChunks.Add(coord);
                interest.StreamingQueueIndex++;
                interest.UnackedChunks++;
                sent++;
            }

            // Queue exhausted — if some chunks were skipped (not ready), reset cursor
            // to re-scan from the beginning next tick. Already-loaded chunks are skipped
            // cheaply via the LoadedChunks HashSet check above.
            // Only fully clear when everything was sent (no skips).
            if (interest.StreamingQueueIndex >= interest.StreamingQueue.Count)
            {
                if (hasSkipped)
                {
                    // Retry from the start — LoadedChunks filter ensures no duplicates
                    interest.StreamingQueueIndex = 0;
                }
                else
                {
                    // All chunks sent successfully — clean slate
                    interest.StreamingQueue.Clear();
                    interest.StreamingScores.Clear();
                    interest.StreamingQueueIndex = 0;
                }
            }
        }

        /// <summary>
        ///     Initializes the streaming queue for a newly connected player centered on their
        ///     spawn chunk. Called when a peer enters Loading state.
        /// </summary>
        public void InitializeForPlayer(PlayerInterestState interest, float3 spawnPosition)
        {
            interest.SpawnPosition = spawnPosition;
            interest.CurrentChunk = WorldToChunk(spawnPosition);
            interest.PreviousChunk = new int3(int.MinValue, int.MinValue, int.MinValue);
            RebuildStreamingQueue(interest);
        }

        /// <summary>
        ///     Rebuilds the streaming queue using spiral ordering from the player's current chunk.
        ///     Excludes chunks already in LoadedChunks. Sorts by Chebyshev distance with
        ///     look-direction bias and Y-level priority (sky chunks deprioritized).
        /// </summary>
        private void RebuildStreamingQueue(PlayerInterestState interest)
        {
            _candidateChunks.Clear();
            _candidateScores.Clear();

            int3 center = interest.CurrentChunk;
            int radius = interest.ViewRadius;
            float2 lookDir = new(
                math.sin(math.radians(interest.LastKnownLookDir.x)),
                math.cos(math.radians(interest.LastKnownLookDir.x)));
            float lookLen = math.length(lookDir);

            if (lookLen > 0.001f)
            {
                lookDir /= lookLen;
            }

            for (int x = -radius; x <= radius; x++)
            {
                for (int z = -radius; z <= radius; z++)
                {
                    for (int y = YLoadMin; y <= YLoadMax; y++)
                    {
                        int3 coord = new(center.x + x, y, center.z + z);

                        if (interest.LoadedChunks.Contains(coord))
                        {
                            continue;
                        }

                        float distance = math.max(math.abs(x), math.abs(z));

                        // Look-direction bias: reduce score for chunks in front of the player
                        float bias = 0f;

                        if (lookLen > 0.001f && (x != 0 || z != 0))
                        {
                            float2 chunkDir = math.normalize(new float2(x, z));
                            bias = -LookBiasWeight * math.dot(chunkDir, lookDir);
                        }

                        // Y-level priority: penalize sky chunks above the player.
                        // Ground-level and below chunks are unaffected (penalty = 0).
                        float yPenalty = math.max(0f, (float)(coord.y - center.y)) * YPriorityWeight;

                        _candidateChunks.Add(coord);
                        _candidateScores.Add(distance + bias + yPenalty);
                    }
                }
            }

            // Schwartzian transform sort by score
            SortByScore(_candidateChunks, _candidateScores);

            interest.StreamingQueue.Clear();
            interest.StreamingQueue.AddRange(_candidateChunks);
            interest.StreamingScores.Clear();
            interest.StreamingScores.AddRange(_candidateScores);
            interest.StreamingQueueIndex = 0;
        }

        /// <summary>
        ///     Computes chunks that should be unloaded (outside view radius) and sends
        ///     unload notifications via the streaming strategy. Only called on chunk boundary crossing.
        /// </summary>
        private void ProcessUnloads(PeerInfo peer, PlayerInterestState interest, IChunkStreamingStrategy strategy)
        {
            _unloadCandidates.Clear();
            int3 center = interest.CurrentChunk;
            int radius = interest.ViewRadius;

            foreach (int3 loaded in interest.LoadedChunks)
            {
                int dx = math.abs(loaded.x - center.x);
                int dz = math.abs(loaded.z - center.z);

                // Unload if outside view radius (with 1 chunk buffer to avoid thrashing)
                if (dx > radius + 1 || dz > radius + 1)
                {
                    _unloadCandidates.Add(loaded);
                }
            }

            for (int i = 0; i < _unloadCandidates.Count; i++)
            {
                int3 coord = _unloadCandidates[i];
                interest.LoadedChunks.Remove(coord);
                strategy.SendUnload(peer, coord);
            }
        }

        /// <summary>
        ///     In-place parallel-array sort by score (insertion sort, stable, no allocation).
        /// </summary>
        private static void SortByScore(List<int3> coords, List<float> scores)
        {
            for (int i = 1; i < coords.Count; i++)
            {
                float scoreI = scores[i];
                int3 coordI = coords[i];
                int j = i - 1;

                while (j >= 0 && scores[j] > scoreI)
                {
                    scores[j + 1] = scores[j];
                    coords[j + 1] = coords[j];
                    j--;
                }

                scores[j + 1] = scoreI;
                coords[j + 1] = coordI;
            }
        }

        private static int3 WorldToChunk(float3 worldPos)
        {
            return new int3(
                (int)math.floor(worldPos.x / ChunkConstants.Size),
                (int)math.floor(worldPos.y / ChunkConstants.Size),
                (int)math.floor(worldPos.z / ChunkConstants.Size));
        }
    }
}
