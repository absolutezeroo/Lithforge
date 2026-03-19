using Lithforge.Runtime.Tick;
using Lithforge.Voxel.Command;

using Unity.Mathematics;

namespace Lithforge.Runtime.Simulation
{
    /// <summary>
    ///     Singleplayer implementation of <see cref="IWorldSimulation" />.
    ///     Owns the tick registry and player physics manager, driving them each tick.
    ///     The tick loop itself (accumulator + while-loop) remains in GameLoop;
    ///     WorldSimulation handles one tick's worth of work per call to <see cref="Tick" />.
    ///     In a future dedicated server, WorldSimulation would be ticked by the
    ///     server's own loop instead of GameLoop.
    /// </summary>
    public sealed class WorldSimulation : IWorldSimulation
    {
        /// <summary>Player ID for the local player in singleplayer mode.</summary>
        private const ushort LocalPlayerId = 0;

        /// <summary>Accumulates per-frame input into discrete per-tick snapshots.</summary>
        private readonly InputSnapshotBuilder _inputSnapshotBuilder;

        /// <summary>Manages the local player's physics body for tick-rate simulation.</summary>
        private readonly PlayerPhysicsManager _playerPhysicsManager;

        /// <summary>
        ///     Ring buffer storing MoveCommands for client-side prediction reconciliation.
        ///     Fed each tick with the local player's predicted position.
        ///     Replay will be wired when network transport is implemented.
        /// </summary>
        private readonly CommandRingBuffer<MoveCommand> _predictionBuffer;

        /// <summary>Registry of all ITickable systems driven each simulation tick.</summary>
        private readonly TickRegistry _tickRegistry;

        /// <summary>
        ///     Per-player monotonic sequence counter for prediction.
        ///     Wraps at 65535 (~36 min at 30 TPS). Reconciliation must use modular
        ///     comparison: sequence B is newer than A when (ushort)(B - A) &lt; 32768.
        ///     Matches MoveCommand.SequenceId wire format (ushort).
        /// </summary>
        private ushort _moveSequenceId;

        /// <summary>Creates a new singleplayer world simulation with the given tick systems.</summary>
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
        ///     Exposes the prediction buffer for future server reconciliation.
        /// </summary>
        public CommandRingBuffer<MoveCommand> PredictionBuffer
        {
            get { return _predictionBuffer; }
        }

        /// <summary>Current server tick number, starting at 1 (tick 0 is the empty sentinel).</summary>
        public uint CurrentTick { get; private set; } = 1;

        /// <summary>
        ///     Advances one tick: consumes input snapshot, drives player physics,
        ///     records a MoveCommand for prediction, ticks all registered systems,
        ///     and increments the tick counter.
        ///     Called by GameLoop once per accumulated tick.
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

                MoveCommand move = new()
                {
                    Tick = CurrentTick,
                    SequenceId = _moveSequenceId++,
                    PlayerId = LocalPlayerId,
                    Position = state.Position,
                    LookDir = new float2(snapshot.Yaw, snapshot.Pitch),
                    Flags = SnapshotToFlags(in snapshot),
                };

                _predictionBuffer.Add(CurrentTick, move);
            }

            _tickRegistry.TickAll(tickDt);

            CurrentTick++;
        }

        /// <summary>
        ///     Packs continuous input fields into the <see cref="InputFlags" /> bitmask.
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
