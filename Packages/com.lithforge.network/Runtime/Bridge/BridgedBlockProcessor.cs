using Lithforge.Network.Server;
using Lithforge.Voxel.Block;

using Unity.Mathematics;

namespace Lithforge.Network.Bridge
{
    /// <summary>
    ///     <see cref="IServerBlockProcessor" /> implementation consumed by <see cref="ServerGameLoop" />
    ///     on the server thread. Each method performs a synchronous round-trip to the main thread
    ///     via the bridge queues. Block commands are rare (~5/sec/player max) so per-call
    ///     synchronization is acceptable.
    /// </summary>
    internal sealed class BridgedBlockProcessor : IServerBlockProcessor
    {
        /// <summary>Shared cross-thread state.</summary>
        private readonly ServerThreadBridge _bridge;

        /// <summary>Creates a bridged block processor backed by the given shared bridge.</summary>
        public BridgedBlockProcessor(ServerThreadBridge bridge)
        {
            _bridge = bridge;
        }

        /// <summary>Synchronous round-trip to add player on main thread.</summary>
        public void AddPlayer(ushort playerId)
        {
            ExecuteRoundTrip(new BlockCommandRequest
            {
                Kind = BlockCommandKind.AddPlayer,
                PlayerId = playerId,
            });
        }

        /// <summary>Synchronous round-trip to remove player on main thread.</summary>
        public void RemovePlayer(ushort playerId)
        {
            ExecuteRoundTrip(new BlockCommandRequest
            {
                Kind = BlockCommandKind.RemovePlayer,
                PlayerId = playerId,
            });
        }

        /// <summary>Synchronous round-trip to refill rate limit tokens on main thread.</summary>
        public void RefillRateLimitTokens(ushort playerId, float currentTime)
        {
            ExecuteRoundTrip(new BlockCommandRequest
            {
                Kind = BlockCommandKind.RefillRateLimitTokens,
                PlayerId = playerId,
                CurrentTime = currentTime,
            });
        }

        /// <summary>Synchronous round-trip to start digging on main thread.</summary>
        public bool StartDigging(ushort playerId, int3 position, float3 playerPosition, uint serverTick)
        {
            BlockCommandResult result = ExecuteRoundTrip(new BlockCommandRequest
            {
                Kind = BlockCommandKind.StartDigging,
                PlayerId = playerId,
                Position = position,
                PlayerPosition = playerPosition,
                ServerTick = serverTick,
            });

            return result.StartDiggingResult;
        }

        /// <summary>Synchronous round-trip to cancel digging on main thread.</summary>
        public void CancelDigging(ushort playerId)
        {
            ExecuteRoundTrip(new BlockCommandRequest
            {
                Kind = BlockCommandKind.CancelDigging,
                PlayerId = playerId,
            });
        }

        /// <summary>Synchronous round-trip to try breaking a block on main thread.</summary>
        public BlockProcessResult TryBreakBlock(
            ushort playerId,
            int3 position,
            float3 playerPosition,
            uint serverTick)
        {
            BlockCommandResult result = ExecuteRoundTrip(new BlockCommandRequest
            {
                Kind = BlockCommandKind.TryBreakBlock,
                PlayerId = playerId,
                Position = position,
                PlayerPosition = playerPosition,
                ServerTick = serverTick,
            });

            return result.ProcessResult;
        }

        /// <summary>Synchronous round-trip to try placing a block on main thread.</summary>
        public BlockProcessResult TryPlaceBlock(
            ushort playerId,
            int3 position,
            StateId blockState,
            BlockFace face,
            float3 playerPosition)
        {
            BlockCommandResult result = ExecuteRoundTrip(new BlockCommandRequest
            {
                Kind = BlockCommandKind.TryPlaceBlock,
                PlayerId = playerId,
                Position = position,
                BlockState = blockState,
                Face = face,
                PlayerPosition = playerPosition,
            });

            return result.ProcessResult;
        }

        /// <summary>Synchronous round-trip to get block state on main thread.</summary>
        public StateId GetBlock(int3 position)
        {
            BlockCommandResult result = ExecuteRoundTrip(new BlockCommandRequest
            {
                Kind = BlockCommandKind.GetBlock,
                Position = position,
            });

            return result.GetBlockResult;
        }

        /// <summary>
        ///     Enqueues a block command, signals the main thread, and waits for the result.
        /// </summary>
        private BlockCommandResult ExecuteRoundTrip(BlockCommandRequest request)
        {
            _bridge.BlockCommandRequests.Enqueue(request);
            _bridge.BlockCommandReady.Release();
            _bridge.BlockCommandComplete.Wait();

            if (_bridge.BlockCommandResults.TryDequeue(out BlockCommandResult result))
            {
                return result;
            }

            return new BlockCommandResult();
        }
    }
}
