using Lithforge.Core.Logging;
using Lithforge.Network.Bridge;
using Lithforge.Network.Server;
using Lithforge.Network.Tests.Mocks;
using Lithforge.Network.Transport;

using NUnit.Framework;

namespace Lithforge.Network.Tests
{
    /// <summary>Tests for <see cref="ServerGameLoop"/> tick accumulation and phase execution.</summary>
    [TestFixture]
    public sealed class ServerGameLoopTickTests
    {
        /// <summary>Mock transport for the NetworkServer.</summary>
        private MockTransport _transport;

        /// <summary>Real NetworkServer wired to the mock transport.</summary>
        private NetworkServer _server;

        /// <summary>Mock simulation tracking tick calls.</summary>
        private MockServerSimulation _simulation;

        /// <summary>Mock block processor (no-op).</summary>
        private MockServerBlockProcessor _blockProcessor;

        /// <summary>Mock chunk provider (no-op).</summary>
        private MockServerChunkProvider _chunkProvider;

        /// <summary>Mock dirty change source tracking flush calls.</summary>
        private MockDirtyChangeSource _dirtyTracker;

        /// <summary>Chunk streaming manager with mock provider.</summary>
        private ChunkStreamingManager _streamingManager;

        /// <summary>Mock streaming strategy (no-op).</summary>
        private MockChunkStreamingStrategy _strategy;

        /// <summary>Readiness waiter with long timeout.</summary>
        private ClientReadinessWaiter _readinessWaiter;

        /// <summary>Server thread bridge (null-safe, unused in synchronous tests).</summary>
        private ServerThreadBridge _bridge;

        /// <summary>Null logger for test isolation.</summary>
        private NullLogger _logger;

        /// <summary>The ServerGameLoop under test.</summary>
        private ServerGameLoop _gameLoop;

        /// <summary>Creates all mocks and wires a ServerGameLoop for testing.</summary>
        [SetUp]
        public void SetUp()
        {
            _logger = new NullLogger();
            _transport = new MockTransport();
            _server = new NetworkServer(_logger, ContentHash.Empty, 16);
            _server.StartWithTransport(_transport);

            _simulation = new MockServerSimulation();
            _blockProcessor = new MockServerBlockProcessor();
            _chunkProvider = new MockServerChunkProvider();
            _dirtyTracker = new MockDirtyChangeSource();
            _strategy = new MockChunkStreamingStrategy();
            _streamingManager = new ChunkStreamingManager(-1, 3, 2, _chunkProvider, _logger);
            _readinessWaiter = new ClientReadinessWaiter(900);
            _bridge = new ServerThreadBridge();

            _gameLoop = new ServerGameLoop(
                _server,
                _simulation,
                _blockProcessor,
                _chunkProvider,
                _dirtyTracker,
                _streamingManager,
                _strategy,
                _readinessWaiter,
                _bridge,
                _logger);
        }

        /// <summary>Disposes the game loop, bridge, and server.</summary>
        [TearDown]
        public void TearDown()
        {
            _gameLoop.Dispose();
            _bridge.Dispose();
            _server.Shutdown();
        }

        /// <summary>Update with 0.1s delta executes 3 ticks at 30 TPS.</summary>
        [Test]
        public void Update_AccumulatesTimeAndExecutesTicks()
        {
            uint startTick = _gameLoop.CurrentTick;

            _gameLoop.Update(0.1f, 0.1f);

            uint ticksExecuted = _gameLoop.CurrentTick - startTick;

            // 0.1s / (1/30) = 3 ticks
            Assert.AreEqual(3u, ticksExecuted, "0.1s at 30 TPS should execute 3 ticks");
        }

        /// <summary>Update with 1.0s delta caps at 5 ticks (spiral-of-death prevention).</summary>
        [Test]
        public void Update_SpiralOfDeathCap_MaxFiveTicks()
        {
            uint startTick = _gameLoop.CurrentTick;

            _gameLoop.Update(1.0f, 1.0f);

            uint ticksExecuted = _gameLoop.CurrentTick - startTick;

            // MaxAccumulatedTime = TickDt * 5 = 5/30 ≈ 0.1667s → 5 ticks max
            Assert.AreEqual(5u, ticksExecuted, "Spiral-of-death cap should limit to 5 ticks");
        }

        /// <summary>ExecuteOneTick calls TickWorldSystems on the simulation exactly once.</summary>
        [Test]
        public void ExecuteOneTick_CallsSimulationTickWorldSystems()
        {
            _gameLoop.ExecuteOneTick(0.1f);

            Assert.AreEqual(1, _simulation.TickWorldSystemsCallCount, "TickWorldSystems should be called once per tick");
        }

        /// <summary>ExecuteOneTick calls FlushAll on the dirty change source exactly once.</summary>
        [Test]
        public void ExecuteOneTick_FlushesDirtyChanges()
        {
            _gameLoop.ExecuteOneTick(0.1f);

            Assert.AreEqual(1, _dirtyTracker.FlushCallCount, "FlushAll should be called once per tick");
        }

        /// <summary>Multiple ticks via Update accumulate simulation calls correctly.</summary>
        [Test]
        public void Update_MultipleTicks_AccumulatesSimulationCalls()
        {
            // 2 ticks worth of time
            _gameLoop.Update(2.0f / 30.0f, 0.1f);

            Assert.AreEqual(2, _simulation.TickWorldSystemsCallCount, "Two ticks should call TickWorldSystems twice");
            Assert.AreEqual(2, _dirtyTracker.FlushCallCount, "Two ticks should call FlushAll twice");
        }
    }
}
