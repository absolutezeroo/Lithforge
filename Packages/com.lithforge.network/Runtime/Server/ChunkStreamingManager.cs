using System.Collections.Generic;

using Lithforge.Core.Logging;
using Lithforge.Network.Connection;
using Lithforge.Network.Messages;
using Lithforge.Voxel.Chunk;

using Unity.Mathematics;

namespace Lithforge.Network.Server
{
    /// <summary>
    ///     Manages per-player chunk streaming: rate limiting, spiral priority ordering,
    ///     look-direction bias, boundary-crossing unloads, and Loading→Playing gating.
    ///     Stateless — all per-player state lives in <see cref="PlayerInterestState" />.
    /// </summary>
    public sealed class ChunkStreamingManager
    {
        /// <summary>Chunks per tick during steady-state play.</summary>
        private const int SteadyStateRate = 2;

        /// <summary>Chunks per tick during initial Loading phase.</summary>
        private const int InitialLoadRate = 4;

        /// <summary>Weight for look-direction bias when scoring chunk priority.</summary>
        private const float LookBiasWeight = 0.4f;
        private readonly List<int3> _candidateChunks = new();
        private readonly List<float> _candidateScores = new();
        private readonly ILogger _logger;
        private readonly HashSet<int3> _newInterestSet = new();
        private readonly int _readyRadius;

        // Cached collections (reused per call, cleared before use)
        private readonly List<int3> _unloadCandidates = new();
        private readonly int _yLoadMax;

        private readonly int _yLoadMin;

        public ChunkStreamingManager(int yLoadMin, int yLoadMax, int readyRadius, ILogger logger)
        {
            _yLoadMin = yLoadMin;
            _yLoadMax = yLoadMax;
            _readyRadius = readyRadius;
            _logger = logger;
        }

        /// <summary>
        ///     Processes chunk streaming for a single peer. Called each tick from ServerGameLoop Phase 5.
        ///     Handles streaming queue rebuild on chunk boundary crossing, chunk unloads, and
        ///     rate-limited chunk data sends.
        /// </summary>
        public void ProcessForPeer(
            PeerInfo peer,
            INetworkServer server,
            IServerChunkProvider chunkProvider,
            uint currentTick)
        {
            PlayerInterestState interest = peer.InterestState;

            if (interest == null)
            {
                return;
            }

            // Detect chunk boundary crossing
            if (!interest.CurrentChunk.Equals(interest.PreviousChunk))
            {
                RebuildStreamingQueue(interest);
                ProcessUnloads(peer, interest, server);
                interest.PreviousChunk = interest.CurrentChunk;
            }

            // Stream chunks up to rate limit
            int rate = interest.IsInitialLoad ? InitialLoadRate : SteadyStateRate;
            int sent = 0;

            while (sent < rate && interest.StreamingQueueIndex < interest.StreamingQueue.Count)
            {
                int3 coord = interest.StreamingQueue[interest.StreamingQueueIndex];

                // Skip if already loaded (can happen if queue was rebuilt while some chunks were in-flight)
                if (interest.LoadedChunks.Contains(coord))
                {
                    interest.StreamingQueueIndex++;
                    continue;
                }

                byte[] chunkData = chunkProvider.SerializeChunk(coord);

                if (chunkData == null)
                {
                    // Chunk not ready on server — skip, will retry next tick
                    interest.StreamingQueueIndex++;
                    continue;
                }

                ChunkDataMessage msg = new()
                {
                    ChunkX = coord.x, ChunkY = coord.y, ChunkZ = coord.z, Payload = chunkData,
                };

                server.SendTo(peer.ConnectionId, msg, PipelineId.FragmentedReliable);
                interest.LoadedChunks.Add(coord);
                interest.StreamingQueueIndex++;
                sent++;
            }

            // Reset queue cursor when exhausted
            if (interest.StreamingQueueIndex >= interest.StreamingQueue.Count)
            {
                interest.StreamingQueue.Clear();
                interest.StreamingScores.Clear();
                interest.StreamingQueueIndex = 0;
            }
        }

        /// <summary>
        ///     Checks whether a peer in Loading state has received enough chunks
        ///     to transition to Playing. Returns true if all chunks within the
        ///     ready radius around the player's current chunk are loaded.
        /// </summary>
        public bool IsReadyForPlaying(PeerInfo peer, IServerChunkProvider chunkProvider)
        {
            PlayerInterestState interest = peer.InterestState;

            if (interest == null)
            {
                return false;
            }

            int3 center = interest.CurrentChunk;

            for (int x = -_readyRadius; x <= _readyRadius; x++)
            {
                for (int z = -_readyRadius; z <= _readyRadius; z++)
                {
                    for (int y = _yLoadMin; y <= _yLoadMax; y++)
                    {
                        int3 coord = new(center.x + x, y, center.z + z);

                        if (!interest.LoadedChunks.Contains(coord))
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
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
        ///     optional look-direction bias.
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
                    for (int y = _yLoadMin; y <= _yLoadMax; y++)
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

                        _candidateChunks.Add(coord);
                        _candidateScores.Add(distance + bias);
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
        ///     ChunkUnloadMessages. Only called on chunk boundary crossing.
        /// </summary>
        private void ProcessUnloads(PeerInfo peer, PlayerInterestState interest, INetworkServer server)
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

                ChunkUnloadMessage msg = new()
                {
                    ChunkX = coord.x, ChunkY = coord.y, ChunkZ = coord.z,
                };

                server.SendTo(peer.ConnectionId, msg, PipelineId.ReliableSequenced);
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
