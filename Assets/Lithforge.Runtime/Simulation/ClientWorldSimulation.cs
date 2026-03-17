using Lithforge.Network;
using Lithforge.Network.Client;
using Lithforge.Network.Messages;
using Lithforge.Runtime.Tick;
using Lithforge.Voxel.Command;

using Unity.Mathematics;

namespace Lithforge.Runtime.Simulation
{
    /// <summary>
    ///     Client-mode implementation of <see cref="IWorldSimulation" />. Adds client-side
    ///     prediction with server reconciliation (Gambetta pattern) on top of the standard
    ///     tick loop. Each tick: captures input, runs local physics prediction, stores the
    ///     predicted state in a ring buffer, sends input to the server, and ticks other systems.
    ///     On receiving a <see cref="PlayerStateMessage" /> from the server, compares the
    ///     authoritative position against the stored prediction and either ignores (noise),
    ///     smooths (small error), reconciles (replay), or teleports (large error).
    /// </summary>
    public sealed class ClientWorldSimulation : IWorldSimulation
    {
        private const float ErrorThresholdIgnore = 0.001f;
        private const float ErrorThresholdSmooth = 0.04f;
        private const float ErrorThresholdTeleport = 4.0f;
        private const float PositionErrorDecay = 0.9f;

        /// <summary>
        ///     Input buffer storing per-tick InputSnapshots for replay during reconciliation.
        ///     Parallel to _predictionBuffer — indexed by the same tick values.
        /// </summary>
        private readonly CommandRingBuffer<InputSnapshot> _inputBuffer;
        private readonly InputSnapshotBuilder _inputSnapshotBuilder;
        private readonly ushort _localPlayerId;
        private readonly INetworkClient _networkClient;
        private readonly PlayerPhysicsManager _playerPhysicsManager;

        /// <summary>Prediction buffer storing per-tick MoveCommands with predicted positions.</summary>
        private readonly CommandRingBuffer<MoveCommand> _predictionBuffer;

        private readonly TickRegistry _tickRegistry;

        private ushort _moveSequenceId;

        // Reconciliation state

        public ClientWorldSimulation(
            TickRegistry tickRegistry,
            PlayerPhysicsManager playerPhysicsManager,
            InputSnapshotBuilder inputSnapshotBuilder,
            INetworkClient networkClient,
            ushort localPlayerId,
            uint initialServerTick)
        {
            _tickRegistry = tickRegistry;
            _playerPhysicsManager = playerPhysicsManager;
            _inputSnapshotBuilder = inputSnapshotBuilder;
            _networkClient = networkClient;
            _localPlayerId = localPlayerId;
            CurrentTick = initialServerTick;
            _predictionBuffer = new CommandRingBuffer<MoveCommand>();
            _inputBuffer = new CommandRingBuffer<InputSnapshot>();
            PositionError = float3.zero;
        }

        /// <summary>
        ///     Visual smoothing offset that decays over time. Applied in GameLoop.LateUpdate
        ///     after position interpolation. Produced by small-error reconciliation.
        /// </summary>
        public float3 PositionError { get; private set; }

        /// <summary>
        ///     Exposes the prediction buffer for testing and debugging.
        /// </summary>
        public CommandRingBuffer<MoveCommand> PredictionBuffer
        {
            get { return _predictionBuffer; }
        }

        public uint CurrentTick { get; private set; }

        /// <summary>
        ///     Advances one tick: captures input, runs local physics prediction,
        ///     stores predicted state, sends input to server, and ticks other systems.
        /// </summary>
        public void Tick(float tickDt)
        {
            // 1. Consume input
            InputSnapshot snapshot = _inputSnapshotBuilder.ConsumeTick();

            // 2. Apply prediction: run physics locally
            if (_playerPhysicsManager != null)
            {
                _playerPhysicsManager.TickPlayer(_localPlayerId, tickDt, in snapshot);

                // 3. Record predicted state
                PlayerPhysicsState state = _playerPhysicsManager.GetState(_localPlayerId);
                MoveCommand move = new()
                {
                    Tick = CurrentTick,
                    SequenceId = _moveSequenceId,
                    PlayerId = _localPlayerId,
                    Position = state.Position,
                    LookDir = new float2(snapshot.Yaw, snapshot.Pitch),
                    Flags = SnapshotToFlags(in snapshot),
                };
                _predictionBuffer.Add(CurrentTick, move);
                _inputBuffer.Add(CurrentTick, snapshot);

                // 4. Send input to server
                MoveInputMessage msg = new()
                {
                    SequenceId = _moveSequenceId, Yaw = snapshot.Yaw, Pitch = snapshot.Pitch, Flags = move.Flags,
                };
                _networkClient.Send(msg, PipelineId.UnreliableSequenced);

                _moveSequenceId++;
            }

            // 5. Decay position error (visual smoothing)
            PositionError *= PositionErrorDecay;

            // 6. Tick all registered systems
            _tickRegistry.TickAll(tickDt);

            CurrentTick++;
        }

        /// <summary>
        ///     Called when a PlayerStateMessage is received from the server.
        ///     Registers as a handler on the client's MessageDispatcher.
        /// </summary>
        public void OnPlayerStateReceived(ConnectionId connId, byte[] data, int offset, int length)
        {
            PlayerStateMessage msg = PlayerStateMessage.Deserialize(data, offset, length);

            if (msg.PlayerId != _localPlayerId)
            {
                // Remote player state — handled by interpolation buffers, not here
                return;
            }

            float3 serverPos = new(msg.PositionX, msg.PositionY, msg.PositionZ);
            ushort ackedSeqId = msg.LastProcessedSeqId;

            // Find the predicted position at the acknowledged sequence ID
            uint ackedTick = 0;
            float3 predictedPos = float3.zero;
            bool foundPrediction = false;

            uint searchStart = CurrentTick > 256 ? CurrentTick - 256 : 1;

            for (uint t = searchStart; t < CurrentTick; t++)
            {
                if (_predictionBuffer.TryGet(t, out MoveCommand predicted) &&
                    predicted.SequenceId == ackedSeqId)
                {
                    predictedPos = predicted.Position;
                    ackedTick = t;
                    foundPrediction = true;
                    break;
                }
            }

            if (!foundPrediction)
            {
                // Too old — buffer has wrapped, nothing to reconcile
                return;
            }

            float error = math.distance(serverPos, predictedPos);

            if (error < ErrorThresholdIgnore)
            {
                // Floating point noise — discard acknowledged entries (inclusive)
                _predictionBuffer.DiscardBefore(ackedTick + 1);
                _inputBuffer.DiscardBefore(ackedTick + 1);
                return;
            }

            if (error < ErrorThresholdSmooth)
            {
                // Small error: visual smoothing only (decays via PositionErrorDecay)
                PositionError += serverPos - predictedPos;
                _predictionBuffer.DiscardBefore(ackedTick + 1);
                _inputBuffer.DiscardBefore(ackedTick + 1);
                return;
            }

            if (error > ErrorThresholdTeleport)
            {
                // Hard teleport: something is fundamentally wrong
                PlayerPhysicsBody body = _playerPhysicsManager.GetBody(_localPlayerId);

                if (body != null)
                {
                    body.Teleport(serverPos);
                }

                PositionError = float3.zero;
                _predictionBuffer.DiscardBefore(CurrentTick);
                _inputBuffer.DiscardBefore(CurrentTick);
                return;
            }

            // Full reconciliation: snap to server state, replay unacked inputs
            FullReconciliation(msg, ackedTick);
        }

        private void FullReconciliation(PlayerStateMessage serverMsg, uint ackedTick)
        {
            PlayerPhysicsBody body = _playerPhysicsManager.GetBody(_localPlayerId);

            if (body == null)
            {
                return;
            }

            // 1. Snap to server state
            float3 serverPos = new(serverMsg.PositionX, serverMsg.PositionY, serverMsg.PositionZ);
            float3 serverVel = new(serverMsg.VelocityX, serverMsg.VelocityY, serverMsg.VelocityZ);
            body.SetPosition(serverPos);
            body.SetVelocity(serverVel);
            body.SetFlags(serverMsg.Flags);

            PositionError = float3.zero;

            // 2. Replay all unacked inputs from (ackedTick+1) to (currentTick-1)
            for (uint t = ackedTick + 1; t < CurrentTick; t++)
            {
                if (_inputBuffer.TryGet(t, out InputSnapshot snapshot))
                {
                    _playerPhysicsManager.TickPlayer(
                        _localPlayerId, FixedTickRate.TickDeltaTime, in snapshot);

                    // Update prediction buffer with corrected position
                    PlayerPhysicsState corrected = _playerPhysicsManager.GetState(_localPlayerId);

                    if (_predictionBuffer.TryGet(t, out MoveCommand oldCmd))
                    {
                        oldCmd.Position = corrected.Position;
                        _predictionBuffer.Add(t, oldCmd);
                    }
                }
            }

            _predictionBuffer.DiscardBefore(ackedTick + 1);
            _inputBuffer.DiscardBefore(ackedTick + 1);
        }

        /// <summary>
        ///     Packs continuous input fields into the InputFlags bitmask.
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
