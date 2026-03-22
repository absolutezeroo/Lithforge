using System;
using System.Collections.Generic;

using Lithforge.Network.Bridge;
using Lithforge.Network.Connection;
using Lithforge.Network.Message;
using Lithforge.Network.Messages;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Command;

using Unity.Mathematics;

namespace Lithforge.Network.Server
{
    /// <summary>
    ///     Processes player inputs and block commands each tick, and handles the
    ///     input-related network message types (MoveInput, PlaceBlockCmd, BreakBlockCmd,
    ///     StartDiggingCmd, SlotClickCmd, TeleportConfirm). For remote clients, validates
    ///     client-submitted positions via <see cref="IServerSimulation.ValidateAndAcceptMove" />
    ///     and issues <see cref="ServerTeleportMessage" /> corrections when violations exceed
    ///     the VL threshold. Internal helper owned by <see cref="ServerGameLoop" />.
    /// </summary>
    internal sealed class ServerInputProcessor
    {
        /// <summary>Number of ticks before a pending teleport confirmation times out.</summary>
        private const uint TeleportTimeoutTicks = 20;

        /// <summary>Block command processor for validating and executing block changes.</summary>
        private readonly IServerBlockProcessor _blockProcessor;

        /// <summary>Bridge for reading the local player's authoritative state (null for dedicated server).</summary>
        private readonly ServerThreadBridge _bridge;

        /// <summary>Delegate returning the current server tick number.</summary>
        private readonly Func<uint> _getCurrentTick;

        /// <summary>The network server interface for sending ACK and teleport messages.</summary>
        private readonly INetworkServer _server;

        /// <summary>Concrete NetworkServer for peer lookup and touch tracking.</summary>
        private readonly NetworkServer _serverImpl;

        /// <summary>Bridge to gameplay simulation for validating and accepting movement.</summary>
        private readonly IServerSimulation _simulation;

        /// <summary>Server-side inventory processor for slot click commands (null until set).</summary>
        private ServerInventoryProcessor _inventoryProcessor;

        /// <summary>Creates a new ServerInputProcessor with all required dependencies.</summary>
        internal ServerInputProcessor(
            INetworkServer server,
            NetworkServer serverImpl,
            IServerSimulation simulation,
            IServerBlockProcessor blockProcessor,
            Func<uint> getCurrentTick,
            ServerThreadBridge bridge)
        {
            _server = server;
            _serverImpl = serverImpl;
            _simulation = simulation;
            _blockProcessor = blockProcessor;
            _getCurrentTick = getCurrentTick;
            _bridge = bridge;
        }

        /// <summary>Injects the server-side inventory processor for slot click command handling.</summary>
        internal void SetInventoryProcessor(ServerInventoryProcessor processor)
        {
            _inventoryProcessor = processor;
        }

        /// <summary>Registers the input-related message handlers on the dispatcher.</summary>
        internal void RegisterMessageHandlers(MessageDispatcher dispatcher)
        {
            dispatcher.RegisterHandler(MessageType.MoveInput, OnMoveInput);
            dispatcher.RegisterHandler(MessageType.PlaceBlockCmd, OnPlaceBlockCmd);
            dispatcher.RegisterHandler(MessageType.BreakBlockCmd, OnBreakBlockCmd);
            dispatcher.RegisterHandler(MessageType.StartDiggingCmd, OnStartDiggingCmd);
            dispatcher.RegisterHandler(MessageType.SlotClickCmd, OnSlotClickCmd);
            dispatcher.RegisterHandler(MessageType.CraftActionCmd, OnCraftActionCmd);
            dispatcher.RegisterHandler(MessageType.ContainerOpenCmd, OnContainerOpenCmd);
            dispatcher.RegisterHandler(MessageType.ContainerCloseCmd, OnContainerCloseCmd);
            dispatcher.RegisterHandler(MessageType.TeleportConfirm, OnTeleportConfirm);
        }

        /// <summary>
        ///     Processes all playing peers' inputs for one tick: validates client-submitted
        ///     positions (or accepts local-peer state directly), updates chunk coordinates,
        ///     and processes block commands. Called once per tick from the facade.
        /// </summary>
        internal void ProcessTick(List<PeerInfo> playingPeers, float tickDt)
        {
            for (int i = 0; i < playingPeers.Count; i++)
            {
                PeerInfo peer = playingPeers[i];
                PlayerInterestState interest = peer.InterestState;

                if (interest is null)
                {
                    continue;
                }

                ushort playerId = peer.AssignedPlayerId;
                uint currentTick = _getCurrentTick();

                PlayerPhysicsState authState;

                // For the local peer (SP/Host), accept the client's authoritative position
                // directly without validation. The host IS the server — no cheating concern.
                if (peer.IsLocal && _bridge?.GetLocalPlayerState() is { } localSnapshot)
                {
                    authState = localSnapshot.State;
                    _simulation.AcceptAuthoritativeState(playerId, authState);

                    // Still drain the move buffer to keep sequence IDs in sync
                    if (interest.MoveBuffer.TryGet(currentTick, out MoveCommand localCmd))
                    {
                        interest.LastProcessedSequenceId = localCmd.SequenceId;
                    }
                }
                else
                {
                    // Remote client: validate the client-submitted position
                    authState = ProcessRemoteMovement(peer, interest, currentTick);
                }

                // Update current chunk from authoritative position
                interest.CurrentChunk = new int3(
                    (int)math.floor(authState.Position.x / ChunkConstants.Size),
                    (int)math.floor(authState.Position.y / ChunkConstants.Size),
                    (int)math.floor(authState.Position.z / ChunkConstants.Size));

                // Process block commands (movement first so reach check uses updated position)
                ProcessBlockCommands(peer, interest, authState, tickDt);
            }
        }

        /// <summary>
        ///     Validates and processes movement for a remote client. If the client is
        ///     awaiting a teleport confirmation, all movement is ignored. Otherwise the
        ///     claimed position is validated and either accepted or triggers a teleport.
        /// </summary>
        private PlayerPhysicsState ProcessRemoteMovement(
            PeerInfo peer,
            PlayerInterestState interest,
            uint currentTick)
        {
            // Check if we are awaiting teleport confirmation
            if (interest.ValidationState.AwaitingTeleportConfirm)
            {
                // Check timeout
                uint elapsed = currentTick - interest.ValidationState.TeleportSentTick;

                if (elapsed > TeleportTimeoutTicks)
                {
                    // Resend the teleport
                    SendTeleport(peer, interest, interest.ValidationState.LastAcceptedPosition, currentTick);
                }

                // Ignore all movement while awaiting confirmation — return last accepted state
                return _simulation.GetPlayerState(peer.AssignedPlayerId);
            }

            // Try to read the latest move command from the buffer
            if (!interest.MoveBuffer.TryGet(currentTick, out MoveCommand moveCmd))
            {
                // No packet this tick — use last known state (stationary)
                return _simulation.GetPlayerState(peer.AssignedPlayerId);
            }

            interest.LastKnownInputFlags = moveCmd.Flags;
            interest.LastKnownLookDir = moveCmd.LookDir;
            interest.LastProcessedSequenceId = moveCmd.SequenceId;

            float3 claimedPosition = moveCmd.Position;

            PlayerPhysicsState authState = _simulation.ValidateAndAcceptMove(
                peer.AssignedPlayerId,
                claimedPosition,
                moveCmd.LookDir.x,
                moveCmd.LookDir.y,
                moveCmd.Flags,
                ref interest.ValidationState,
                out bool needsTeleport);

            if (needsTeleport)
            {
                SendTeleport(peer, interest, interest.ValidationState.LastAcceptedPosition, currentTick);
            }

            return authState;
        }

        /// <summary>Sends a ServerTeleportMessage to the peer and sets the awaiting state.</summary>
        private void SendTeleport(
            PeerInfo peer,
            PlayerInterestState interest,
            float3 position,
            uint currentTick)
        {
            interest.ValidationState.PendingTeleportId++;
            interest.ValidationState.AwaitingTeleportConfirm = true;
            interest.ValidationState.TeleportSentTick = currentTick;

            ServerTeleportMessage msg = new()
            {
                TeleportId = interest.ValidationState.PendingTeleportId,
                PositionX = position.x,
                PositionY = position.y,
                PositionZ = position.z,
            };

            _server.SendTo(peer.ConnectionId, msg, PipelineId.ReliableSequenced);
        }

        /// <summary>
        ///     Drains pending StartDigging, BreakBlock, and PlaceBlock commands for a single
        ///     peer, validating each through the block processor and sending ACKs.
        /// </summary>
        private void ProcessBlockCommands(
            PeerInfo peer,
            PlayerInterestState interest,
            PlayerPhysicsState playerState,
            float tickDt)
        {
            ushort playerId = peer.AssignedPlayerId;
            uint currentTick = _getCurrentTick();
            _blockProcessor.RefillRateLimitTokens(playerId, currentTick * tickDt);

            // Process StartDigging commands first (must precede break commands in same tick)
            for (int i = 0; i < interest.PendingStartDiggingCommands.Count; i++)
            {
                StartDiggingCommand cmd = interest.PendingStartDiggingCommands[i];
                _blockProcessor.StartDigging(playerId, cmd.Position, playerState.Position, currentTick);
            }

            interest.PendingStartDiggingCommands.Clear();

            // Process break commands
            for (int i = 0; i < interest.PendingBreakCommands.Count; i++)
            {
                BreakBlockCommand cmd = interest.PendingBreakCommands[i];

                BlockProcessResult result = _blockProcessor.TryBreakBlock(
                    playerId, cmd.Position, playerState.Position, currentTick);

                AcknowledgeBlockChangeMessage ack = new()
                {
                    SequenceId = cmd.SequenceId,
                    Accepted = (byte)(result.Accepted ? 1 : 0),
                    PositionX = cmd.Position.x,
                    PositionY = cmd.Position.y,
                    PositionZ = cmd.Position.z,
                    CorrectedState = result.Accepted
                        ? result.AcceptedState.Value
                        : _blockProcessor.GetBlock(cmd.Position).Value,
                };

                _server.SendTo(peer.ConnectionId, ack, PipelineId.ReliableSequenced);
            }

            interest.PendingBreakCommands.Clear();

            // Process place commands
            for (int i = 0; i < interest.PendingPlaceCommands.Count; i++)
            {
                PlaceBlockCommand cmd = interest.PendingPlaceCommands[i];

                BlockProcessResult result = _blockProcessor.TryPlaceBlock(
                    playerId, cmd.Position, cmd.BlockState, cmd.Face, playerState.Position);

                AcknowledgeBlockChangeMessage ack = new()
                {
                    SequenceId = cmd.SequenceId,
                    Accepted = (byte)(result.Accepted ? 1 : 0),
                    PositionX = cmd.Position.x,
                    PositionY = cmd.Position.y,
                    PositionZ = cmd.Position.z,
                    CorrectedState = result.Accepted
                        ? result.AcceptedState.Value
                        : _blockProcessor.GetBlock(cmd.Position).Value,
                };

                _server.SendTo(peer.ConnectionId, ack, PipelineId.ReliableSequenced);
            }

            interest.PendingPlaceCommands.Clear();
        }

        /// <summary>Handles MoveInput messages, buffering the command with client-reported position.</summary>
        private void OnMoveInput(ConnectionId connId, byte[] data, int offset, int length)
        {
            _serverImpl.TouchPeer(connId);
            PeerInfo peer = _serverImpl.GetPeer(connId);

            if (peer?.InterestState is null ||
                peer.StateMachine.Current != ConnectionState.Playing)
            {
                return;
            }

            uint currentTick = _getCurrentTick();
            MoveInputMessage msg = MoveInputMessage.Deserialize(data, offset, length);
            MoveCommand cmd = new()
            {
                Tick = currentTick,
                SequenceId = msg.SequenceId,
                PlayerId = peer.AssignedPlayerId,
                Position = new float3(msg.PositionX, msg.PositionY, msg.PositionZ),
                LookDir = new float2(msg.Yaw, msg.Pitch),
                Flags = msg.Flags,
            };

            peer.InterestState.MoveBuffer.Add(currentTick, cmd);
        }

        /// <summary>Handles TeleportConfirm messages, clearing the awaiting state if IDs match.</summary>
        private void OnTeleportConfirm(ConnectionId connId, byte[] data, int offset, int length)
        {
            _serverImpl.TouchPeer(connId);
            PeerInfo peer = _serverImpl.GetPeer(connId);

            if (peer?.InterestState is null ||
                peer.StateMachine.Current != ConnectionState.Playing)
            {
                return;
            }

            TeleportConfirmMessage msg = TeleportConfirmMessage.Deserialize(data, offset, length);

            if (peer.InterestState.ValidationState.AwaitingTeleportConfirm &&
                peer.InterestState.ValidationState.PendingTeleportId == msg.TeleportId)
            {
                PlayerMovementValidator.OnTeleportConfirmed(ref peer.InterestState.ValidationState);
            }
        }

        /// <summary>Handles PlaceBlockCmd messages, queuing the command for Phase 2 block processing.</summary>
        private void OnPlaceBlockCmd(ConnectionId connId, byte[] data, int offset, int length)
        {
            _serverImpl.TouchPeer(connId);
            PeerInfo peer = _serverImpl.GetPeer(connId);

            if (peer?.InterestState is null ||
                peer.StateMachine.Current != ConnectionState.Playing)
            {
                return;
            }

            uint currentTick = _getCurrentTick();
            PlaceBlockCmdMessage msg = PlaceBlockCmdMessage.Deserialize(data, offset, length);

            PlaceBlockCommand cmd = new()
            {
                Tick = currentTick,
                SequenceId = msg.SequenceId,
                PlayerId = peer.AssignedPlayerId,
                Position = new int3(msg.PositionX, msg.PositionY, msg.PositionZ),
                BlockState = new StateId(msg.BlockState),
                Face = (BlockFace)msg.Face,
            };

            peer.InterestState.PendingPlaceCommands.Add(cmd);
        }

        /// <summary>Handles BreakBlockCmd messages, queuing the command for Phase 2 block processing.</summary>
        private void OnBreakBlockCmd(ConnectionId connId, byte[] data, int offset, int length)
        {
            _serverImpl.TouchPeer(connId);
            PeerInfo peer = _serverImpl.GetPeer(connId);

            if (peer?.InterestState is null ||
                peer.StateMachine.Current != ConnectionState.Playing)
            {
                return;
            }

            uint currentTick = _getCurrentTick();
            BreakBlockCmdMessage msg = BreakBlockCmdMessage.Deserialize(data, offset, length);

            BreakBlockCommand cmd = new()
            {
                Tick = currentTick, SequenceId = msg.SequenceId, PlayerId = peer.AssignedPlayerId, Position = new int3(msg.PositionX, msg.PositionY, msg.PositionZ),
            };

            peer.InterestState.PendingBreakCommands.Add(cmd);
        }

        /// <summary>Handles StartDiggingCmd messages, queuing the command for Phase 2 block processing.</summary>
        private void OnStartDiggingCmd(ConnectionId connId, byte[] data, int offset, int length)
        {
            _serverImpl.TouchPeer(connId);
            PeerInfo peer = _serverImpl.GetPeer(connId);

            if (peer?.InterestState is null ||
                peer.StateMachine.Current != ConnectionState.Playing)
            {
                return;
            }

            uint currentTick = _getCurrentTick();
            StartDiggingCmdMessage msg = StartDiggingCmdMessage.Deserialize(data, offset, length);

            StartDiggingCommand cmd = new()
            {
                Tick = currentTick, SequenceId = msg.SequenceId, PlayerId = peer.AssignedPlayerId, Position = new int3(msg.PositionX, msg.PositionY, msg.PositionZ),
            };

            peer.InterestState.PendingStartDiggingCommands.Add(cmd);
        }

        /// <summary>Handles a slot click command from a client, delegating to the inventory processor.</summary>
        private void OnSlotClickCmd(ConnectionId connId, byte[] data, int offset, int length)
        {
            _serverImpl.TouchPeer(connId);
            PeerInfo peer = _serverImpl.GetPeer(connId);

            if (peer?.InterestState is null
                || peer.StateMachine.Current != ConnectionState.Playing)
            {
                return;
            }

            if (_inventoryProcessor is null)
            {
                return;
            }

            SlotClickCmdMessage cmd = SlotClickCmdMessage.Deserialize(data, offset, length);
            InventorySyncMessage? response = _inventoryProcessor.ProcessSlotClick(peer, cmd);

            if (response.HasValue)
            {
                _server.SendTo(connId, response.Value, PipelineId.ReliableSequenced);
            }
        }

        /// <summary>Handles a craft action command from a client.</summary>
        private void OnCraftActionCmd(ConnectionId connId, byte[] data, int offset, int length)
        {
            _serverImpl.TouchPeer(connId);
            PeerInfo peer = _serverImpl.GetPeer(connId);

            if (peer?.InterestState is null
                || peer.StateMachine.Current != ConnectionState.Playing)
            {
                return;
            }

            if (_inventoryProcessor is null)
            {
                return;
            }

            CraftActionCmdMessage cmd = CraftActionCmdMessage.Deserialize(data, offset, length);
            _inventoryProcessor.ProcessCraftAction(peer, cmd);
        }

        /// <summary>Handles a container open command from a client.</summary>
        private void OnContainerOpenCmd(ConnectionId connId, byte[] data, int offset, int length)
        {
            _serverImpl.TouchPeer(connId);
            PeerInfo peer = _serverImpl.GetPeer(connId);

            if (peer?.InterestState is null
                || peer.StateMachine.Current != ConnectionState.Playing)
            {
                return;
            }

            if (_inventoryProcessor is null)
            {
                return;
            }

            ContainerOpenCmdMessage cmd = ContainerOpenCmdMessage.Deserialize(data, offset, length);
            _inventoryProcessor.ProcessContainerOpen(peer, cmd);
        }

        /// <summary>Handles a container close command from a client.</summary>
        private void OnContainerCloseCmd(ConnectionId connId, byte[] data, int offset, int length)
        {
            _serverImpl.TouchPeer(connId);
            PeerInfo peer = _serverImpl.GetPeer(connId);

            if (peer?.InterestState is null
                || peer.StateMachine.Current != ConnectionState.Playing)
            {
                return;
            }

            if (_inventoryProcessor is null)
            {
                return;
            }

            ContainerCloseCmdMessage cmd = ContainerCloseCmdMessage.Deserialize(data, offset, length);
            _inventoryProcessor.ProcessContainerClose(peer, cmd);
        }
    }
}
