using System.Collections.Generic;

using Lithforge.Network;
using Lithforge.Network.Client;
using Lithforge.Network.Message;
using Lithforge.Network.Messages;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;

using Unity.Mathematics;

namespace Lithforge.Runtime.Network
{
    /// <summary>
    ///     Client-side optimistic block prediction. When the player places or breaks a block,
    ///     the change is applied locally immediately and the command is sent to the server.
    ///     On receiving <see cref="AcknowledgeBlockChangeMessage" />, accepted predictions are
    ///     discarded; rejected predictions are reverted to the server's corrected state.
    /// </summary>
    public sealed class ClientBlockPredictor
    {
        private readonly ChunkManager _chunkManager;
        private readonly List<int3> _dirtiedChunksCache = new();
        private readonly INetworkClient _networkClient;
        private readonly Dictionary<ushort, PendingPrediction> _pending = new();

        private ushort _sequenceId;

        public ClientBlockPredictor(
            ChunkManager chunkManager,
            INetworkClient networkClient)
        {
            _chunkManager = chunkManager;
            _networkClient = networkClient;

            MessageDispatcher dispatcher = networkClient.Dispatcher;
            dispatcher.RegisterHandler(
                MessageType.AcknowledgeBlockChange, OnAcknowledge);
        }

        /// <summary>
        ///     Sends a StartDigging notification to the server. This must be called
        ///     when the player begins mining a block so the server can track timing
        ///     for break speed validation. No local prediction is needed — the actual
        ///     break is predicted when mining completes via <see cref="PredictBreak" />.
        /// </summary>
        public void SendStartDigging(int3 position)
        {
            if (!_networkClient.IsPlaying)
            {
                return;
            }

            ushort seqId = _sequenceId++;

            StartDiggingCmdMessage msg = new()
            {
                SequenceId = seqId, PositionX = position.x, PositionY = position.y, PositionZ = position.z,
            };

            _networkClient.Send(msg, PipelineId.ReliableSequenced);
        }

        /// <summary>
        ///     Optimistically places a block locally and sends the command to the server.
        /// </summary>
        public void PredictPlace(int3 position, StateId newState, byte face)
        {
            if (!_networkClient.IsPlaying)
            {
                return;
            }

            StateId oldState = _chunkManager.GetBlock(position);
            ushort seqId = _sequenceId++;

            // Apply optimistically
            _dirtiedChunksCache.Clear();
            _chunkManager.SetBlock(position, newState, _dirtiedChunksCache);

            // Record prediction for potential revert
            _pending[seqId] = new PendingPrediction
            {
                Position = position, OldState = oldState,
            };

            // Send command to server
            PlaceBlockCmdMessage msg = new()
            {
                SequenceId = seqId,
                PositionX = position.x,
                PositionY = position.y,
                PositionZ = position.z,
                BlockState = newState.Value,
                Face = face,
            };

            _networkClient.Send(msg, PipelineId.ReliableSequenced);
        }

        /// <summary>
        ///     Optimistically breaks a block locally and sends the command to the server.
        /// </summary>
        public void PredictBreak(int3 position)
        {
            if (!_networkClient.IsPlaying)
            {
                return;
            }

            StateId oldState = _chunkManager.GetBlock(position);
            ushort seqId = _sequenceId++;

            // Apply optimistically (set to air)
            _dirtiedChunksCache.Clear();
            _chunkManager.SetBlock(position, StateId.Air, _dirtiedChunksCache);

            // Record prediction for potential revert
            _pending[seqId] = new PendingPrediction
            {
                Position = position, OldState = oldState,
            };

            // Send command to server
            BreakBlockCmdMessage msg = new()
            {
                SequenceId = seqId, PositionX = position.x, PositionY = position.y, PositionZ = position.z,
            };

            _networkClient.Send(msg, PipelineId.ReliableSequenced);
        }

        private void OnAcknowledge(ConnectionId connId, byte[] data, int offset, int length)
        {
            AcknowledgeBlockChangeMessage msg =
                AcknowledgeBlockChangeMessage.Deserialize(data, offset, length);

            if (!_pending.TryGetValue(msg.SequenceId, out PendingPrediction prediction))
            {
                return;
            }

            _pending.Remove(msg.SequenceId);

            if (msg.Accepted != 0)
            {
                // Server accepted — prediction was correct, nothing to do
                return;
            }

            // Server rejected — revert to corrected state
            StateId correctedState = new(msg.CorrectedState);
            _dirtiedChunksCache.Clear();
            _chunkManager.SetBlock(prediction.Position, correctedState, _dirtiedChunksCache);
        }
    }
}
