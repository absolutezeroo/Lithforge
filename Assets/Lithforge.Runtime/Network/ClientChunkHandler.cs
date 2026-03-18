using System;
using System.Collections.Generic;
using Lithforge.Network;
using Lithforge.Network.Client;
using Lithforge.Network.Message;
using Lithforge.Network.Messages;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.Network.Chunk;
using Unity.Collections;
using Unity.Mathematics;

namespace Lithforge.Runtime.Network
{
    /// <summary>
    /// Client-side handler for chunk lifecycle messages from the server.
    /// Registers on the client's <see cref="MessageDispatcher"/> and processes
    /// ChunkData, ChunkUnload, BlockChange, MultiBlockChange, and GameReady messages.
    /// Owns persistent NativeArray buffers for deserialization to avoid per-chunk allocation.
    ///
    /// Chunk unloads are queued rather than applied immediately, because the full
    /// cleanup chain (mesh store, schedulers, biome tint) lives in GameLoop.
    /// Call <see cref="DrainPendingUnloads"/> from GameLoop each frame.
    /// </summary>
    public sealed class ClientChunkHandler : IDisposable
    {
        private readonly ChunkManager _chunkManager;
        private readonly Action<GameReadyMessage> _onGameReady;

        private NativeArray<StateId> _deserializeVoxelBuffer;
        private NativeArray<byte> _deserializeLightBuffer;
        private bool _disposed;

        // Cached list for multi-block change deserialization
        private readonly List<int3> _dirtiedChunksCache = new();

        // Pending unloads — queued by network handler, drained by GameLoop
        private readonly List<int3> _pendingUnloads = new();

        public ClientChunkHandler(
            ChunkManager chunkManager,
            INetworkClient networkClient,
            Action<GameReadyMessage> onGameReady)
        {
            _chunkManager = chunkManager;
            _onGameReady = onGameReady;

            _deserializeVoxelBuffer = new NativeArray<StateId>(
                ChunkConstants.Volume, Allocator.Persistent);
            _deserializeLightBuffer = new NativeArray<byte>(
                ChunkConstants.Volume, Allocator.Persistent);

            MessageDispatcher dispatcher = networkClient.Dispatcher;
            dispatcher.RegisterHandler(MessageType.ChunkData, OnChunkData);
            dispatcher.RegisterHandler(MessageType.ChunkUnload, OnChunkUnload);
            dispatcher.RegisterHandler(MessageType.BlockChange, OnBlockChange);
            dispatcher.RegisterHandler(MessageType.MultiBlockChange, OnMultiBlockChange);
            dispatcher.RegisterHandler(MessageType.GameReady, OnGameReady);
        }

        /// <summary>
        /// Drains any pending chunk unload coordinates into the provided list.
        /// GameLoop calls this each frame and runs the full cleanup chain
        /// (mesh store, schedulers, etc.) for each coordinate.
        /// </summary>
        public void DrainPendingUnloads(List<int3> result)
        {
            result.Clear();

            if (_pendingUnloads.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _pendingUnloads.Count; i++)
            {
                result.Add(_pendingUnloads[i]);
            }

            _pendingUnloads.Clear();
        }

        private void OnChunkData(ConnectionId connId, byte[] data, int offset, int length)
        {
            ChunkDataMessage msg = ChunkDataMessage.Deserialize(data, offset, length);
            int3 chunkCoord = new(msg.ChunkX, msg.ChunkY, msg.ChunkZ);

            if (msg.Payload == null || msg.Payload.Length == 0)
            {
                return;
            }

            bool ok = ChunkNetSerializer.DeserializeFullChunk(
                msg.Payload, _deserializeVoxelBuffer, _deserializeLightBuffer);

            if (!ok)
            {
                return;
            }

            // Load chunk into ChunkManager from network data.
            // The chunk skips generation/decoration and goes directly to Generated state
            // so the MeshScheduler can pick it up for meshing.
            _chunkManager.LoadFromNetwork(chunkCoord, _deserializeVoxelBuffer, _deserializeLightBuffer);
        }

        private void OnChunkUnload(ConnectionId connId, byte[] data, int offset, int length)
        {
            ChunkUnloadMessage msg = ChunkUnloadMessage.Deserialize(data, offset, length);
            int3 chunkCoord = new(msg.ChunkX, msg.ChunkY, msg.ChunkZ);

            // Queue for GameLoop to process with full cleanup chain
            _pendingUnloads.Add(chunkCoord);
        }

        private void OnBlockChange(ConnectionId connId, byte[] data, int offset, int length)
        {
            BlockChangeMessage msg = BlockChangeMessage.Deserialize(data, offset, length);
            int3 position = new(msg.PositionX, msg.PositionY, msg.PositionZ);
            StateId newState = new(msg.NewState);
            _dirtiedChunksCache.Clear();
            _chunkManager.SetBlock(position, newState, _dirtiedChunksCache);
        }

        private void OnMultiBlockChange(ConnectionId connId, byte[] data, int offset, int length)
        {
            MultiBlockChangeMessage msg = MultiBlockChangeMessage.Deserialize(data, offset, length);

            if (msg.BatchData == null || msg.BatchData.Length == 0)
            {
                return;
            }

            bool ok = ChunkNetSerializer.DeserializeBlockChangeBatch(
                msg.BatchData, out int3 chunkCoord, out List<BlockChangeEntry> changes);

            if (!ok || changes == null)
            {
                return;
            }

            _dirtiedChunksCache.Clear();

            for (int i = 0; i < changes.Count; i++)
            {
                BlockChangeEntry change = changes[i];
                _chunkManager.SetBlock(change.Position, change.NewState, _dirtiedChunksCache);
            }
        }

        private void OnGameReady(ConnectionId connId, byte[] data, int offset, int length)
        {
            GameReadyMessage msg = GameReadyMessage.Deserialize(data, offset, length);
            _onGameReady?.Invoke(msg);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                if (_deserializeVoxelBuffer.IsCreated)
                {
                    _deserializeVoxelBuffer.Dispose();
                }

                if (_deserializeLightBuffer.IsCreated)
                {
                    _deserializeLightBuffer.Dispose();
                }
            }
        }
    }
}
