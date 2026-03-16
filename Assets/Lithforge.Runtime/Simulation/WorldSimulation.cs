using Lithforge.Runtime.Tick;

namespace Lithforge.Runtime.Simulation
{
    /// <summary>
    /// Singleplayer implementation of <see cref="IWorldSimulation"/>.
    /// Owns the tick registry and player physics body, driving them each tick.
    /// The tick loop itself (accumulator + while-loop) remains in GameLoop;
    /// WorldSimulation handles one tick's worth of work per call to <see cref="Tick"/>.
    ///
    /// In a future dedicated server, WorldSimulation would be ticked by the
    /// server's own loop instead of GameLoop.
    /// </summary>
    public sealed class WorldSimulation : IWorldSimulation
    {
        private readonly TickRegistry _tickRegistry;
        private readonly PlayerPhysicsBody _playerPhysicsBody;
        private readonly InputSnapshotBuilder _inputSnapshotBuilder;

        // Starts at 1 so tick 0 remains the "empty slot" sentinel in CommandRingBuffer.
        private uint _currentTick = 1;

        public uint CurrentTick
        {
            get { return _currentTick; }
        }

        public WorldSimulation(
            TickRegistry tickRegistry,
            PlayerPhysicsBody playerPhysicsBody,
            InputSnapshotBuilder inputSnapshotBuilder)
        {
            _tickRegistry = tickRegistry;
            _playerPhysicsBody = playerPhysicsBody;
            _inputSnapshotBuilder = inputSnapshotBuilder;
        }

        /// <summary>
        /// Advances one tick: consumes input snapshot, drives player physics,
        /// ticks all registered systems, and increments the tick counter.
        /// Called by GameLoop once per accumulated tick.
        /// </summary>
        public void Tick(float tickDt)
        {
            InputSnapshot snapshot = _inputSnapshotBuilder.ConsumeTick();

            if (_playerPhysicsBody != null)
            {
                _playerPhysicsBody.TickWithSnapshot(tickDt, in snapshot);
            }

            _tickRegistry.TickAll(tickDt);

            _currentTick++;
        }
    }
}
