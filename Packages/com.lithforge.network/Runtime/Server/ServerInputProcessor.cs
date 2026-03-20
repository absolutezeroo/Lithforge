using System;
using System.Collections.Generic;

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
    ///     Processes player inputs and block commands each tick, and handles the 5
    ///     input-related network message types (MoveInput, PlaceBlockCmd, BreakBlockCmd,
    ///     StartDiggingCmd, SlotClickCmd). Internal helper owned by <see cref="ServerGameLoop" />.
    /// </summary>
    internal sealed class ServerInputProcessor
    {
        /// <summary>Block command processor for validating and executing block changes.</summary>
        private readonly IServerBlockProcessor _blockProcessor;

        /// <summary>Delegate returning the current server tick number.</summary>
        private readonly Func<uint> _getCurrentTick;

        /// <summary>The network server interface for sending ACK messages.</summary>
        private readonly INetworkServer _server;

        /// <summary>Concrete NetworkServer for peer lookup and touch tracking.</summary>
        private readonly NetworkServer _serverImpl;

        /// <summary>Bridge to gameplay simulation for applying movement.</summary>
        private readonly IServerSimulation _simulation;

        /// <summary>Server-side inventory processor for slot click commands (null until set).</summary>
        private ServerInventoryProcessor _inventoryProcessor;

        /// <summary>Creates a new ServerInputProcessor with all required dependencies.</summary>
        internal ServerInputProcessor(
            INetworkServer server,
            NetworkServer serverImpl,
            IServerSimulation simulation,
            IServerBlockProcessor blockProcessor,
            Func<uint> getCurrentTick)
        {
            _server = server;
            _serverImpl = serverImpl;
            _simulation = simulation;
            _blockProcessor = blockProcessor;
            _getCurrentTick = getCurrentTick;
        }

        /// <summary>Injects the server-side inventory processor for slot click command handling.</summary>
        internal void SetInventoryProcessor(ServerInventoryProcessor processor)
        {
            _inventoryProcessor = processor;
        }

        /// <summary>Registers the 5 input-related message handlers on the dispatcher.</summary>
        internal void RegisterMessageHandlers(MessageDispatcher dispatcher)
        {
            dispatcher.RegisterHandler(MessageType.MoveInput, OnMoveInput);
            dispatcher.RegisterHandler(MessageType.PlaceBlockCmd, OnPlaceBlockCmd);
            dispatcher.RegisterHandler(MessageType.BreakBlockCmd, OnBreakBlockCmd);
            dispatcher.RegisterHandler(MessageType.StartDiggingCmd, OnStartDiggingCmd);
            dispatcher.RegisterHandler(MessageType.SlotClickCmd, OnSlotClickCmd);
        }

        /// <summary>
        ///     Processes all playing peers' inputs for one tick: drains move buffers,
        ///     applies authoritative movement, updates chunk coordinates, and processes
        ///     block commands. Called once per tick from the facade.
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

                // Process movement: try to get buffered input for this tick
                float yaw;
                float pitch;
                byte flags;
                ushort seqId;

                if (interest.MoveBuffer.TryGet(currentTick, out MoveCommand moveCmd))
                {
                    yaw = moveCmd.LookDir.x;
                    pitch = moveCmd.LookDir.y;
                    flags = moveCmd.Flags;
                    seqId = moveCmd.SequenceId;
                    interest.LastKnownInputFlags = flags;
                    interest.LastKnownLookDir = moveCmd.LookDir;
                    interest.LastProcessedSequenceId = seqId;
                }
                else
                {
                    // Input repeat: use last known input (player continues moving)
                    yaw = interest.LastKnownLookDir.x;
                    pitch = interest.LastKnownLookDir.y;
                    flags = interest.LastKnownInputFlags;
                    seqId = interest.LastProcessedSequenceId;
                }

                PlayerPhysicsState authState = _simulation.ApplyMoveInput(
                    playerId, yaw, pitch, flags, tickDt);

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

        /// <summary>Handles MoveInput messages, buffering the command for the owning peer's tick processing.</summary>
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
                Position = float3.zero, // Server computes position authoritatively
                LookDir = new float2(msg.Yaw, msg.Pitch),
                Flags = msg.Flags,
            };

            peer.InterestState.MoveBuffer.Add(currentTick, cmd);
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
    }
}
