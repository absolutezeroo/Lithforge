using System;

using Lithforge.Network;
using Lithforge.Network.Client;
using Lithforge.Network.Messages;
using Lithforge.Runtime.Tick;
using Lithforge.Voxel.Command;

using Unity.Mathematics;

using ILogger = Lithforge.Core.Logging.ILogger;

namespace Lithforge.Runtime.Simulation
{
    /// <summary>
    ///     Client-mode implementation of <see cref="IWorldSimulation" />. Uses client-authoritative
    ///     movement: each tick captures input, runs local physics, and sends the resulting position
    ///     to the server for validation. The server either accepts the position (echoed back in
    ///     <see cref="PlayerStateMessage" />) or issues a <see cref="ServerTeleportMessage" />
    ///     correction. No prediction buffers, no replay-based reconciliation.
    /// </summary>
    public sealed class ClientWorldSimulation : IWorldSimulation
    {
        /// <summary>Accumulates per-frame input into discrete per-tick snapshots.</summary>
        private readonly InputSnapshotBuilder _inputSnapshotBuilder;

        /// <summary>Network player ID assigned to this client by the server.</summary>
        private readonly ushort _localPlayerId;

        /// <summary>Logger for movement diagnostics.</summary>
        private readonly ILogger _logger;

        /// <summary>Network client for sending input and teleport confirm messages to the server.</summary>
        private readonly INetworkClient _networkClient;

        /// <summary>Manages the local player's physics body for client-side movement.</summary>
        private readonly PlayerPhysicsManager _playerPhysicsManager;

        /// <summary>
        ///     When true, the server is in the same process (SP/Host via DirectTransport).
        ///     TickRegistry is gated to avoid double-ticking world systems already driven by the server.
        /// </summary>
        private readonly bool _serverIsLocal;

        /// <summary>Registry of all ITickable systems driven each simulation tick.</summary>
        private readonly TickRegistry _tickRegistry;

        /// <summary>Per-player monotonic sequence counter for input messages, wraps at 65535.</summary>
        private ushort _moveSequenceId;

        /// <summary>
        ///     Optional callback invoked for remote player state messages (PlayerId != localPlayerId).
        ///     Set via <see cref="SetRemotePlayerStateHandler" /> after construction.
        /// </summary>
        private Action<PlayerStateMessage> _onRemotePlayerState;

        /// <summary>Creates a new client-mode world simulation with client-authoritative movement.</summary>
        public ClientWorldSimulation(
            TickRegistry tickRegistry,
            PlayerPhysicsManager playerPhysicsManager,
            InputSnapshotBuilder inputSnapshotBuilder,
            INetworkClient networkClient,
            ushort localPlayerId,
            uint initialServerTick,
            bool serverIsLocal = false,
            ILogger logger = null)
        {
            _tickRegistry = tickRegistry;
            _playerPhysicsManager = playerPhysicsManager;
            _logger = logger;
            _inputSnapshotBuilder = inputSnapshotBuilder;
            _networkClient = networkClient;
            _localPlayerId = localPlayerId;
            _serverIsLocal = serverIsLocal;
            CurrentTick = initialServerTick;
        }

        /// <summary>Current client-side tick number, initialized from the server's tick on connection.</summary>
        public uint CurrentTick { get; private set; }

        /// <summary>
        ///     Advances one tick: captures input, runs local physics, sends the resulting
        ///     position to the server, and ticks other systems.
        /// </summary>
        public void Tick(float tickDt)
        {
            // 1. Consume input
            InputSnapshot snapshot = _inputSnapshotBuilder.ConsumeTick();

            // 2. Run physics locally (zero-latency movement for the player).
            if (_playerPhysicsManager is not null)
            {
                _playerPhysicsManager.TickPlayer(_localPlayerId, tickDt, in snapshot);

                // 3. Read resulting position and send to server for validation
                PlayerPhysicsState state = _playerPhysicsManager.GetState(_localPlayerId);

                if (_networkClient.IsPlaying)
                {
                    MoveInputMessage msg = new()
                    {
                        SequenceId = _moveSequenceId,
                        Yaw = snapshot.Yaw,
                        Pitch = snapshot.Pitch,
                        Flags = SnapshotToFlags(in snapshot),
                        PositionX = state.Position.x,
                        PositionY = state.Position.y,
                        PositionZ = state.Position.z,
                    };
                    _networkClient.Send(msg, PipelineId.UnreliableSequenced);
                }

                _moveSequenceId++;
            }

            // 4. Tick all registered systems.
            // In SP/Host (_serverIsLocal) the server already ran TickRegistry.TickAll via
            // ServerSimulation.TickWorldSystems — running it again would double-tick every ITickable.
            if (!_serverIsLocal)
            {
                _tickRegistry.TickAll(tickDt);
            }

            CurrentTick++;
        }

        /// <summary>
        ///     Registers a handler that will be called for remote player state messages
        ///     (where PlayerId != localPlayerId). Used to route remote player snapshots
        ///     to the <see cref="Player.RemotePlayerManager" /> without requiring a second
        ///     handler registration on the single-handler MessageDispatcher.
        /// </summary>
        public void SetRemotePlayerStateHandler(Action<PlayerStateMessage> handler)
        {
            _onRemotePlayerState = handler;
        }

        /// <summary>
        ///     Called when a PlayerStateMessage is received from the server. For the local
        ///     player this is an informational ACK only — position corrections are handled
        ///     exclusively by <see cref="OnServerTeleportReceived" />. Comparing the echoed
        ///     position against our current position would always show latency-induced drift
        ///     (the echo is from several ticks ago while we have moved since), so we do not
        ///     snap here. Remote player states are routed to the interpolation manager.
        /// </summary>
        public void OnPlayerStateReceived(ConnectionId connId, byte[] data, int offset, int length)
        {
            PlayerStateMessage msg = PlayerStateMessage.Deserialize(data, offset, length);

            if (msg.PlayerId != _localPlayerId)
            {
                // Remote player state — route to remote player manager
                _onRemotePlayerState?.Invoke(msg);
            }
        }

        /// <summary>
        ///     Called when a ServerTeleportMessage is received from the server. Immediately
        ///     teleports the local player to the corrected position and sends a confirmation
        ///     back so the server resumes accepting movement.
        /// </summary>
        public void OnServerTeleportReceived(ConnectionId connId, byte[] data, int offset, int length)
        {
            ServerTeleportMessage msg = ServerTeleportMessage.Deserialize(data, offset, length);
            float3 teleportPos = new(msg.PositionX, msg.PositionY, msg.PositionZ);

            _logger?.LogInfo(
                $"[MOVE] server teleport: id={msg.TeleportId} pos=({teleportPos.x:F2},{teleportPos.y:F2},{teleportPos.z:F2})");

            PlayerPhysicsBody body = _playerPhysicsManager.GetBody(_localPlayerId);
            body?.Teleport(teleportPos);

            // Acknowledge the teleport so the server resumes accepting our movement
            TeleportConfirmMessage confirm = new() { TeleportId = msg.TeleportId };
            _networkClient.Send(confirm, PipelineId.ReliableSequenced);
        }

        /// <summary>
        ///     Packs input fields into the InputFlags bitmask.
        ///     Bits 0-5: held/continuous, Bits 6-7: edge-triggered toggles.
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

            if (snapshot.FlyTogglePressed)
            {
                flags |= InputFlags.FlyToggle;
            }

            if (snapshot.NoclipTogglePressed)
            {
                flags |= InputFlags.NoclipToggle;
            }

            return flags;
        }
    }
}
