using Lithforge.Runtime.Tick;
using Lithforge.Voxel.Command;

namespace Lithforge.Runtime.Simulation
{
    /// <summary>
    /// Singleplayer implementation of <see cref="IWorldSimulation"/>.
    /// Owns the tick registry and player physics manager, driving them each tick.
    /// The tick loop itself (accumulator + while-loop) remains in GameLoop;
    /// WorldSimulation handles one tick's worth of work per call to <see cref="Tick"/>.
    ///
    /// In a future dedicated server, WorldSimulation would be ticked by the
    /// server's own loop instead of GameLoop.
    /// </summary>
    public sealed class WorldSimulation : IWorldSimulation
    {
        private const ushort LocalPlayerId = 0;

        private readonly TickRegistry _tickRegistry;
        private readonly PlayerPhysicsManager _playerPhysicsManager;
        private readonly InputSnapshotBuilder _inputSnapshotBuilder;

        /// <summary>
        /// Ring buffer storing MoveCommands for client-side prediction reconciliation.
        /// Fed each tick with the local player's predicted position.
        /// Replay will be wired when network transport is implemented.
        /// </summary>
        private readonly CommandRingBuffer<MoveCommand> _predictionBuffer;

        /// <summary>
        /// Per-player monotonic sequence counter for prediction.
        /// Wraps at 65535 (~36 min at 30 TPS). Reconciliation must use modular
        /// comparison: sequence B is newer than A when (ushort)(B - A) &lt; 32768.
        /// Matches MoveCommand.SequenceId wire format (ushort).
        /// </summary>
        private ushort _moveSequenceId;

        // Starts at 1 so tick 0 remains the "empty slot" sentinel in CommandRingBuffer.
        private uint _currentTick = 1;

        public uint CurrentTick
        {
            get { return _currentTick; }
        }

        /// <summary>
        /// Exposes the prediction buffer for future server reconciliation.
        /// </summary>
        public CommandRingBuffer<MoveCommand> PredictionBuffer
        {
            get { return _predictionBuffer; }
        }

        public WorldSimulation(
            TickRegistry tickRegistry,
            PlayerPhysicsManager playerPhysicsManager,
            InputSnapshotBuilder inputSnapshotBuilder)
        {
            _tickRegistry = tickRegistry;
            _playerPhysicsManager = playerPhysicsManager;
            _inputSnapshotBuilder = inputSnapshotBuilder;
            _predictionBuffer = new CommandRingBuffer<MoveCommand>();
        }

        /// <summary>
        /// Advances one tick: consumes input snapshot, drives player physics,
        /// records a MoveCommand for prediction, ticks all registered systems,
        /// and increments the tick counter.
        /// Called by GameLoop once per accumulated tick.
        /// </summary>
        public void Tick(float tickDt)
        {
            InputSnapshot snapshot = _inputSnapshotBuilder.ConsumeTick();

            if (_playerPhysicsManager != null)
            {
                _playerPhysicsManager.TickPlayer(LocalPlayerId, tickDt, in snapshot);

                // Record predicted move for client-side prediction reconciliation
                PlayerPhysicsState state =
                    _playerPhysicsManager.GetState(LocalPlayerId);

                MoveCommand move = new MoveCommand
                {
                    Tick = _currentTick,
                    SequenceId = _moveSequenceId++,
                    PlayerId = LocalPlayerId,
                    Position = state.Position,
                    LookDir = new Unity.Mathematics.float2(snapshot.Yaw, snapshot.Pitch),
                    Flags = SnapshotToFlags(in snapshot),
                };

                _predictionBuffer.Add(_currentTick, move);
            }

            _tickRegistry.TickAll(tickDt);

            _currentTick++;
        }

        /// <summary>
        /// Packs continuous input fields into the <see cref="InputFlags"/> bitmask.
        /// </summary>
        private static byte SnapshotToFlags(in InputSnapshot snapshot)
        {
            byte flags = 0;

            if (snapshot.MoveForward)
            {
                flags |= InputFlags.MoveForward;
            }

            if (snapshot.MoveBack)
            {
                flags |= InputFlags.MoveBack;
            }

            if (snapshot.MoveLeft)
            {
                flags |= InputFlags.MoveLeft;
            }

            if (snapshot.MoveRight)
            {
                flags |= InputFlags.MoveRight;
            }

            if (snapshot.Sprint)
            {
                flags |= InputFlags.Sprint;
            }

            if (snapshot.JumpPressed || snapshot.JumpHeld)
            {
                flags |= InputFlags.Jump;
            }

            return flags;
        }
    }
}
