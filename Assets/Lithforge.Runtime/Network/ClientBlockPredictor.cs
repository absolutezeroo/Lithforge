using System.Collections.Generic;

using Lithforge.Network;
using Lithforge.Network.Client;
using Lithforge.Network.Message;
using Lithforge.Network.Messages;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;

using Unity.Mathematics;

using UnityEngine;

namespace Lithforge.Runtime.Network
{
    /// <summary>
    ///     Client-side optimistic block prediction. When the player places or breaks a block,
    ///     the change is applied locally immediately and the command is sent to the server.
    ///     On receiving <see cref="AcknowledgeBlockChangeMessage" />, accepted predictions are
    ///     discarded; rejected predictions are reverted to the server's corrected state.
    ///     Unacknowledged predictions are expired after <see cref="NetworkConstants.PredictionExpirySeconds" />
    ///     to prevent unbounded growth of the pending map.
    /// </summary>
    public sealed class ClientBlockPredictor
    {
        private readonly ChunkManager _chunkManager;

        private readonly List<int3> _dirtiedChunksCache = new();

        /// <summary>Reusable key list for the expiry sweep (fill pattern, no per-tick allocation).</summary>
        private readonly List<ushort> _expiredKeys = new();

        private readonly INetworkClient _networkClient;

        private readonly Dictionary<ushort, PendingPrediction> _pending = new();

        /// <summary>
        ///     Secondary index: position → oldest OldState for O(1) collision override lookups.
        ///     Tracks the original state before the first prediction at each coordinate.
        ///     Removed when the last prediction at that position is acknowledged or expired.
        /// </summary>
        private readonly Dictionary<int3, OriginalStateEntry> _originalStateByPosition = new();

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
        ///     Returns true when the predictor is ready to accept commands (network
        ///     connection is in <c>Playing</c> state). Callers should check this before
        ///     invoking <see cref="PredictPlace" /> or <see cref="PredictBreak" /> to
        ///     avoid silently dropping commands.
        /// </summary>
        public bool IsReady
        {
            get { return _networkClient.IsPlaying; }
        }

        /// <summary>
        ///     Returns true if the given world position has an unconfirmed prediction,
        ///     and outputs the original (pre-prediction) state. Used by the collision
        ///     system to resolve collisions against server-confirmed state, not
        ///     optimistically-applied predictions. O(1) via position-keyed secondary index.
        /// </summary>
        public bool TryGetOriginalState(int3 position, out StateId originalState)
        {
            if (_originalStateByPosition.TryGetValue(position, out OriginalStateEntry entry))
            {
                originalState = entry.OldState;

                return true;
            }

            originalState = default;

            return false;
        }

        /// <summary>
        ///     Called each fixed tick (30 TPS). Expires predictions older than
        ///     <see cref="NetworkConstants.PredictionExpirySeconds" /> by reverting the block
        ///     to its pre-prediction state, preventing unbounded pending map growth.
        /// </summary>
        public void Tick(float currentRealtime)
        {
            if (_pending.Count == 0)
            {
                return;
            }

            _expiredKeys.Clear();

            foreach (KeyValuePair<ushort, PendingPrediction> pair in _pending)
            {
                if (currentRealtime - pair.Value.Timestamp >= NetworkConstants.PredictionExpirySeconds)
                {
                    _expiredKeys.Add(pair.Key);
                }
            }

            for (int i = 0; i < _expiredKeys.Count; i++)
            {
                ushort key = _expiredKeys[i];
                PendingPrediction prediction = _pending[key];
                _pending.Remove(key);
                UntrackOriginalState(prediction.Position);

                _dirtiedChunksCache.Clear();
                _chunkManager.SetBlock(prediction.Position, prediction.OldState, _dirtiedChunksCache);
            }
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
                Position = position, OldState = oldState, Timestamp = Time.realtimeSinceStartup,
            };

            // Track original state for collision override (only first prediction at this position)
            TrackOriginalState(position, oldState);

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
                Position = position, OldState = oldState, Timestamp = Time.realtimeSinceStartup,
            };

            // Track original state for collision override (only first prediction at this position)
            TrackOriginalState(position, oldState);

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
            UntrackOriginalState(prediction.Position);

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

        /// <summary>
        ///     Records the original (pre-prediction) state for a position in the secondary index.
        ///     Only the first prediction at a given coordinate stores its OldState; subsequent
        ///     predictions at the same position increment the reference count without changing
        ///     the stored state, ensuring collision always resolves against the server-confirmed state.
        /// </summary>
        private void TrackOriginalState(int3 position, StateId oldState)
        {
            if (_originalStateByPosition.TryGetValue(position, out OriginalStateEntry existing))
            {
                existing.RefCount++;
                _originalStateByPosition[position] = existing;
            }
            else
            {
                _originalStateByPosition[position] = new OriginalStateEntry
                {
                    OldState = oldState,
                    RefCount = 1,
                };
            }
        }

        /// <summary>
        ///     Decrements the reference count for a position in the secondary index.
        ///     Removes the entry when no more predictions reference that position.
        /// </summary>
        private void UntrackOriginalState(int3 position)
        {
            if (!_originalStateByPosition.TryGetValue(position, out OriginalStateEntry existing))
            {
                return;
            }

            existing.RefCount--;

            if (existing.RefCount <= 0)
            {
                _originalStateByPosition.Remove(position);
            }
            else
            {
                _originalStateByPosition[position] = existing;
            }
        }

        /// <summary>Tracks the original block state and prediction count at a world position.</summary>
        private struct OriginalStateEntry
        {
            /// <summary>The block state before any predictions were applied at this position.</summary>
            public StateId OldState;

            /// <summary>Number of active predictions referencing this position.</summary>
            public int RefCount;
        }
    }
}