using Lithforge.Network.Server;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Command;

using Unity.Mathematics;

namespace Lithforge.Network.Tests.Mocks
{
    /// <summary>Minimal mock of <see cref="IServerBlockProcessor" /> for testing ServerGameLoop.</summary>
    internal sealed class MockServerBlockProcessor : IServerBlockProcessor
    {
        /// <summary>Adds a player (no-op).</summary>
        public void AddPlayer(ushort playerId)
        {
        }

        /// <summary>Removes a player (no-op).</summary>
        public void RemovePlayer(ushort playerId)
        {
        }

        /// <summary>Refills rate limit tokens (no-op).</summary>
        public void RefillRateLimitTokens(ushort playerId, float currentTime)
        {
        }

        /// <summary>Records start of digging (no-op, returns false).</summary>
        public bool StartDigging(ushort playerId, int3 position, float3 playerPosition, uint serverTick)
        {
            return false;
        }

        /// <summary>Cancels digging (no-op).</summary>
        public void CancelDigging(ushort playerId)
        {
        }

        /// <summary>Returns a rejected block break result.</summary>
        public BlockProcessResult TryBreakBlock(ushort playerId, int3 position, float3 playerPosition, uint serverTick)
        {
            return BlockProcessResult.Reject(CommandResult.ChunkNotReady, StateId.Air);
        }

        /// <summary>Returns a rejected block place result.</summary>
        public BlockProcessResult TryPlaceBlock(ushort playerId, int3 position, StateId blockState, BlockFace face, float3 playerPosition)
        {
            return BlockProcessResult.Reject(CommandResult.ChunkNotReady, StateId.Air);
        }

        /// <summary>Returns air for any position.</summary>
        public StateId GetBlock(int3 position)
        {
            return StateId.Air;
        }
    }
}
