using System.Collections.Generic;

using Lithforge.Core.Logging;
using Lithforge.Network.Server;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Command;

using Unity.Mathematics;

namespace Lithforge.Runtime.Simulation
{
    /// <summary>
    ///     Tier 3 implementation of <see cref="IServerBlockProcessor" />. Validates all 8
    ///     server-side block command checks and calls <see cref="ChunkManager.SetBlock" />
    ///     on accepted commands. Owns per-player rate limiting and digging state.
    ///     Validation order for break: rate limit → reach → chunk loaded → block type →
    ///     sequence (StartDigging match) → break time → player position → execute.
    ///     Validation order for place: rate limit → reach → chunk loaded → block type
    ///     (target air/fluid) → player position → placement face → execute.
    /// </summary>
    public sealed class ServerBlockProcessor : IServerBlockProcessor
    {
        /// <summary>Maximum distance in blocks a player can reach to interact with a block.</summary>
        private const float MaxReachDistance = 6f;

        /// <summary>Extra tolerance on reach checks to account for client prediction drift.</summary>
        private const float PositionTolerance = 3f;

        /// <summary>Maximum tokens in the per-player rate limiter bucket.</summary>
        private const float MaxTokens = 20f;

        /// <summary>Rate at which tokens refill per second.</summary>
        private const float RefillRate = 20f;

        /// <summary>Token cost per block operation (break or place).</summary>
        private const float CostPerOp = 1f;

        /// <summary>Fraction of expected break time below which a FinishDigging is rejected as too fast.</summary>
        private const float BreakTimeTolerance = 0.5f;

        /// <summary>Duration of one server tick in seconds (1/30 at 30 TPS).</summary>
        private const float TickDt = 1f / 30f;

        /// <summary>Chunk manager for block reads/writes.</summary>
        private readonly ChunkManager _chunkManager;

        /// <summary>Per-player digging state tracking StartDigging position and timing.</summary>
        private readonly Dictionary<ushort, PlayerDiggingState> _diggingStates = new();

        /// <summary>Reusable list for SetBlock dirtied chunks (fill pattern).</summary>
        private readonly List<int3> _dirtiedChunksCache = new();

        /// <summary>Mining speed multiplier when using bare hands (no tool).</summary>
        private readonly float _handMiningMultiplier;

        /// <summary>Logger for validation warnings and diagnostics.</summary>
        private readonly ILogger _logger;

        /// <summary>Burst-accessible state registry for block flag lookups.</summary>
        private readonly NativeStateRegistry _nativeStateRegistry;

        /// <summary>Per-player timestamp of last token refill for rate limiting.</summary>
        private readonly Dictionary<ushort, float> _rateLimitLastRefill = new();

        /// <summary>Per-player current token count for rate limiting block operations.</summary>
        private readonly Dictionary<ushort, float> _rateLimitTokens = new();

        /// <summary>Managed state registry for looking up block hardness and properties.</summary>
        private readonly StateRegistry _stateRegistry;

        /// <summary>Creates a new server block processor with the given dependencies.</summary>
        public ServerBlockProcessor(
            ChunkManager chunkManager,
            StateRegistry stateRegistry,
            NativeStateRegistry nativeStateRegistry,
            float handMiningMultiplier,
            ILogger logger)
        {
            _chunkManager = chunkManager;
            _stateRegistry = stateRegistry;
            _nativeStateRegistry = nativeStateRegistry;
            _handMiningMultiplier = handMiningMultiplier;
            _logger = logger;
        }

        /// <summary>Initializes rate limit tokens and digging state for a newly connected player.</summary>
        public void AddPlayer(ushort playerId)
        {
            _diggingStates[playerId] = new PlayerDiggingState();
            _rateLimitTokens[playerId] = MaxTokens;
            _rateLimitLastRefill[playerId] = 0f;
        }

        /// <summary>Removes all per-player state (digging, rate limit) for a disconnecting player.</summary>
        public void RemovePlayer(ushort playerId)
        {
            _diggingStates.Remove(playerId);
            _rateLimitTokens.Remove(playerId);
            _rateLimitLastRefill.Remove(playerId);
        }

        /// <summary>Refills the player's rate limit tokens based on elapsed time since last refill.</summary>
        public void RefillRateLimitTokens(ushort playerId, float currentTime)
        {
            if (!_rateLimitLastRefill.TryGetValue(playerId, out float lastRefill))
            {
                return;
            }

            float elapsed = currentTime - lastRefill;

            if (elapsed <= 0f)
            {
                return;
            }

            float current = _rateLimitTokens[playerId];
            float refilled = math.min(MaxTokens, current + elapsed * RefillRate);
            _rateLimitTokens[playerId] = refilled;
            _rateLimitLastRefill[playerId] = currentTime;
        }

        /// <summary>Validates and begins tracking a digging action at the given position.</summary>
        public bool StartDigging(
            ushort playerId,
            int3 position,
            float3 playerPosition,
            uint serverTick)
        {
            // Reach check
            float3 blockCenter = new(position.x + 0.5f, position.y + 0.5f, position.z + 0.5f);
            float distance = math.distance(playerPosition, blockCenter);

            if (distance > MaxReachDistance + PositionTolerance)
            {
                return false;
            }

            // Chunk loaded check
            int3 chunkCoord = ChunkManager.WorldToChunk(position);
            ManagedChunk chunk = _chunkManager.GetChunk(chunkCoord);

            if (chunk == null || chunk.State < ChunkState.Generated)
            {
                return false;
            }

            // Read block state
            StateId stateId = _chunkManager.GetBlock(position);

            if (stateId.Value == 0)
            {
                return false; // Can't mine air
            }

            // Compute expected break time from hardness (hand-mining = worst case)
            float expectedBreakTime = 0f;
            StateRegistryEntry entry = _stateRegistry.GetEntryForState(stateId);

            if (entry != null)
            {
                if (entry.Hardness < 0f)
                {
                    return false; // Unbreakable (bedrock)
                }

                expectedBreakTime = entry.Hardness * _handMiningMultiplier;
            }

            PlayerDiggingState digState = new()
            {
                IsDigging = true, DigPosition = position, DigStartTick = serverTick, ExpectedBreakTime = expectedBreakTime,
            };

            _diggingStates[playerId] = digState;
            return true;
        }

        /// <summary>Clears the digging state for the given player (e.g. when they switch targets).</summary>
        public void CancelDigging(ushort playerId)
        {
            if (_diggingStates.ContainsKey(playerId))
            {
                PlayerDiggingState cleared = new();
                _diggingStates[playerId] = cleared;
            }
        }

        /// <summary>Validates and executes a block break, returning Accept with the old/new state or a rejection code.</summary>
        public BlockProcessResult TryBreakBlock(
            ushort playerId,
            int3 position,
            float3 playerPosition,
            uint serverTick)
        {
            StateId currentState = _chunkManager.GetBlock(position);

            // 1. Rate limit
            if (!ConsumeToken(playerId))
            {
                return BlockProcessResult.Reject(CommandResult.RateLimited, currentState);
            }

            // 2. Reach check
            float3 blockCenter = new(position.x + 0.5f, position.y + 0.5f, position.z + 0.5f);
            float distance = math.distance(playerPosition, blockCenter);

            if (distance > MaxReachDistance)
            {
                return BlockProcessResult.Reject(CommandResult.OutOfRange, currentState);
            }

            // 3. Chunk loaded check
            int3 chunkCoord = ChunkManager.WorldToChunk(position);
            ManagedChunk chunk = _chunkManager.GetChunk(chunkCoord);

            if (chunk == null || chunk.State < ChunkState.Generated)
            {
                return BlockProcessResult.Reject(CommandResult.ChunkNotReady, currentState);
            }

            // 4. Block type check — can't break air or unbreakable
            if (currentState.Value == 0)
            {
                return BlockProcessResult.Reject(CommandResult.BlockNotFound, currentState);
            }

            StateRegistryEntry entry = _stateRegistry.GetEntryForState(currentState);

            if (entry is
                {
                    Hardness: < 0f,
                })
            {
                return BlockProcessResult.Reject(CommandResult.NotBreakable, currentState);
            }

            // 5. Sequence validation — StartDigging must precede at same position
            if (!_diggingStates.TryGetValue(playerId, out PlayerDiggingState digState) ||
                !digState.IsDigging ||
                !digState.DigPosition.Equals(position))
            {
                return BlockProcessResult.Reject(CommandResult.InvalidAction, currentState);
            }

            // 6. Break time validation
            float elapsedSeconds = (serverTick - digState.DigStartTick) * TickDt;
            float minimumRequired = digState.ExpectedBreakTime * BreakTimeTolerance;

            if (elapsedSeconds < minimumRequired)
            {
                return BlockProcessResult.Reject(CommandResult.RateLimited, currentState);
            }

            // 7. Player position validation (generous tolerance for prediction drift)
            if (distance > MaxReachDistance + PositionTolerance)
            {
                return BlockProcessResult.Reject(CommandResult.OutOfRange, currentState);
            }

            // 8. Execute — set block to air
            _dirtiedChunksCache.Clear();
            _chunkManager.SetBlock(position, StateId.Air, _dirtiedChunksCache);

            // Clear digging state
            PlayerDiggingState cleared = new();
            _diggingStates[playerId] = cleared;

            return BlockProcessResult.Accept(currentState, StateId.Air);
        }

        /// <summary>Validates and executes a block placement, returning Accept with the old/new state or a rejection code.</summary>
        public BlockProcessResult TryPlaceBlock(
            ushort playerId,
            int3 position,
            StateId blockState,
            BlockFace face,
            float3 playerPosition)
        {
            StateId currentState = _chunkManager.GetBlock(position);

            // 1. Rate limit
            if (!ConsumeToken(playerId))
            {
                return BlockProcessResult.Reject(CommandResult.RateLimited, currentState);
            }

            // 2. Reach check
            float3 blockCenter = new(position.x + 0.5f, position.y + 0.5f, position.z + 0.5f);
            float distance = math.distance(playerPosition, blockCenter);

            if (distance > MaxReachDistance)
            {
                return BlockProcessResult.Reject(CommandResult.OutOfRange, currentState);
            }

            // 3. Chunk loaded check
            int3 chunkCoord = ChunkManager.WorldToChunk(position);
            ManagedChunk chunk = _chunkManager.GetChunk(chunkCoord);

            if (chunk == null || chunk.State < ChunkState.Generated)
            {
                return BlockProcessResult.Reject(CommandResult.ChunkNotReady, currentState);
            }

            // 4. Block type check — target must be air or fluid
            if (currentState.Value != 0)
            {
                BlockStateCompact compact = _nativeStateRegistry.States[currentState.Value];

                if ((compact.Flags & BlockStateCompact.FlagFluid) == 0)
                {
                    return BlockProcessResult.Reject(CommandResult.TargetOccupied, currentState);
                }
            }

            // 5. Player position validation
            if (distance > MaxReachDistance + PositionTolerance)
            {
                return BlockProcessResult.Reject(CommandResult.OutOfRange, currentState);
            }

            // 6. Placement face validation — adjacent block in clicked direction must be non-air
            int3 adjacentCoord = position - FaceNormalToInt3(face);
            StateId adjacentState = _chunkManager.GetBlock(adjacentCoord);

            if (adjacentState.Value == 0)
            {
                return BlockProcessResult.Reject(CommandResult.InvalidAction, currentState);
            }

            // 7. Player overlap check — don't place a solid block where the player is standing
            if (blockState.Value > 0 && _nativeStateRegistry.States.IsCreated &&
                blockState.Value < _nativeStateRegistry.States.Length)
            {
                BlockStateCompact placedCompact = _nativeStateRegistry.States[blockState.Value];

                if ((placedCompact.Flags & BlockStateCompact.FlagFullCube) != 0)
                {
                    // Simple AABB check: player is ~0.6 wide, 1.8 tall
                    float3 blockMin = new(position.x, position.y, position.z);
                    float3 blockMax = blockMin + new float3(1f, 1f, 1f);
                    float3 playerMin = playerPosition - new float3(0.3f, 0f, 0.3f);
                    float3 playerMax = playerPosition + new float3(0.3f, 1.8f, 0.3f);

                    bool overlaps = blockMin.x < playerMax.x && blockMax.x > playerMin.x &&
                                    blockMin.y < playerMax.y && blockMax.y > playerMin.y &&
                                    blockMin.z < playerMax.z && blockMax.z > playerMin.z;

                    if (overlaps)
                    {
                        return BlockProcessResult.Reject(CommandResult.PlayerOverlap, currentState);
                    }
                }
            }

            // 8. Execute — place the block
            _dirtiedChunksCache.Clear();
            _chunkManager.SetBlock(position, blockState, _dirtiedChunksCache);

            return BlockProcessResult.Accept(currentState, blockState);
        }

        /// <summary>Returns the block state at the given world position.</summary>
        public StateId GetBlock(int3 position)
        {
            return _chunkManager.GetBlock(position);
        }

        /// <summary>Attempts to consume one token from the player's rate limit bucket. Returns false if empty.</summary>
        private bool ConsumeToken(ushort playerId)
        {
            if (!_rateLimitTokens.TryGetValue(playerId, out float tokens))
            {
                return false;
            }

            if (tokens < CostPerOp)
            {
                return false;
            }

            _rateLimitTokens[playerId] = tokens - CostPerOp;
            return true;
        }

        /// <summary>Converts a BlockFace enum value to its corresponding direction vector.</summary>
        private static int3 FaceNormalToInt3(BlockFace face)
        {
            switch (face)
            {
                case BlockFace.East:
                    return new int3(1, 0, 0);
                case BlockFace.West:
                    return new int3(-1, 0, 0);
                case BlockFace.Up:
                    return new int3(0, 1, 0);
                case BlockFace.Down:
                    return new int3(0, -1, 0);
                case BlockFace.North:
                    return new int3(0, 0, 1);
                case BlockFace.South:
                    return new int3(0, 0, -1);
                default:
                    return int3.zero;
            }
        }
    }
}
