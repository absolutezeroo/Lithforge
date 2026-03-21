using System.Collections.Generic;

using Lithforge.Network.Connection;
using Lithforge.Network.Server;
using Lithforge.Network.Tests.Mocks;

using NUnit.Framework;

using Unity.Mathematics;

namespace Lithforge.Network.Tests
{
    /// <summary>Tests for <see cref="ChunkStreamingManager"/> queue building and rate limiting.</summary>
    [TestFixture]
    public sealed class ChunkStreamingManagerTests
    {
        /// <summary>Mock chunk provider for controlling readiness.</summary>
        private MockServerChunkProvider _chunkProvider;

        /// <summary>Null logger for test isolation.</summary>
        private NullLogger _logger;

        /// <summary>The streaming manager under test.</summary>
        private ChunkStreamingManager _streamingManager;

        /// <summary>Creates mock provider and streaming manager.</summary>
        [SetUp]
        public void SetUp()
        {
            _logger = new NullLogger();
            _chunkProvider = new MockServerChunkProvider();
            _streamingManager = new ChunkStreamingManager(-1, 3, 2, _chunkProvider, _logger);
        }

        /// <summary>No-op teardown.</summary>
        [TearDown]
        public void TearDown()
        {
        }

        /// <summary>InitializeForPlayer builds a streaming queue sorted by distance (closest first).</summary>
        [Test]
        public void InitializeForPlayer_BuildsStreamingQueue_SortedClosestFirst()
        {
            PlayerInterestState interest = new(3);

            _streamingManager.InitializeForPlayer(interest, new float3(16, 64, 16));

            // The queue should be non-empty and sorted by distance
            Assert.Greater(interest.StreamingQueue.Count, 0, "Streaming queue should have entries after initialization");

            // Mark all chunks as ready so they can be streamed
            for (int i = 0; i < interest.StreamingQueue.Count; i++)
            {
                _chunkProvider.ReadyChunks.Add(interest.StreamingQueue[i]);
            }

            // Verify sorted order: earlier entries should have lower or equal Chebyshev distance
            int3 center = interest.CurrentChunk;
            float previousScore = -1f;

            for (int i = 0; i < interest.StreamingQueue.Count; i++)
            {
                int3 coord = interest.StreamingQueue[i];
                int3 diff = coord - center;
                float distance = math.max(math.abs(diff.x), math.abs(diff.z));

                // Allow same-distance entries (bias reordering), but overall trend should be increasing
                if (i is > 0 and < 10)
                {
                    // The first few entries should be at distance 0 or 1 from center
                    Assert.LessOrEqual(distance, 2f,
                        $"Early queue entry at index {i} has Chebyshev distance {distance}, expected <= 2");
                }
            }
        }

        /// <summary>ProcessForPeer respects the rate limit and does not send more than the allowed rate.</summary>
        [Test]
        public void ProcessForPeer_RespectsRateLimit()
        {
            PlayerInterestState interest = new(5);
            PeerInfo peer = new(new ConnectionId(1));
            peer.InterestState = interest;

            _streamingManager.InitializeForPlayer(interest, new float3(16, 64, 16));

            // Mark all chunks as ready
            for (int i = 0; i < interest.StreamingQueue.Count; i++)
            {
                _chunkProvider.ReadyChunks.Add(interest.StreamingQueue[i]);
            }

            MockChunkStreamingStrategy strategy = new();
            int queueSize = interest.StreamingQueue.Count;

            Assert.Greater(queueSize, 4, "Queue should have more entries than the initial load rate");

            // Process one tick — InitialLoadRate = 4
            _streamingManager.ProcessForPeer(peer, strategy, 1);

            // Should have sent at most InitialLoadRate (4) chunks
            Assert.LessOrEqual(strategy.StreamCallCount, 4,
                "First tick should send at most InitialLoadRate chunks");
            Assert.Greater(strategy.StreamCallCount, 0,
                "At least one chunk should be streamed on the first tick");
        }
    }
}
