using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Command;

using Unity.Mathematics;

namespace Lithforge.Network.Server
{
    /// <summary>
    ///     Validates client-submitted positions server-side for the client-authoritative
    ///     movement model. Checks: max speed per tick, noclip (AABB overlap with solid
    ///     blocks), and unauthorised flight. Maintains violation level (VL) per player
    ///     via <see cref="PlayerValidationState" /> which the caller owns and passes by ref.
    ///     All checks are synchronous — no Burst, no jobs.
    /// </summary>
    public sealed class PlayerMovementValidator
    {
        /// <summary>
        ///     Maximum squared distance a player may travel in one tick before triggering
        ///     a speed violation. Sprint speed (5.612 m/s) at 30 TPS = 0.187 blocks/tick.
        ///     With 3× margin for diagonal + jump arc: (0.187 × 3)² ≈ 0.315. We use 1.0
        ///     (equivalent to 1 block/tick linear) to absorb latency jitter generously.
        /// </summary>
        private const float MaxDistanceSqPerTick = 1.0f;

        /// <summary>VL added per detected speed violation.</summary>
        private const float VlPerSpeedViolation = 5f;

        /// <summary>VL added per detected noclip (inside solid block) violation.</summary>
        private const float VlPerNoclipViolation = 10f;

        /// <summary>VL added per tick of unauthorised airborne ascent.</summary>
        private const float VlPerFlightViolation = 3f;

        /// <summary>VL subtracted each clean tick (linear decay).</summary>
        private const float VlDecayPerTick = 0.5f;

        /// <summary>VL threshold above which a teleport correction is issued.</summary>
        private const float VlTeleportThreshold = 20f;

        /// <summary>Player AABB half-width (X/Z) for noclip testing.</summary>
        private const float PlayerHalfWidth = 0.3f;

        /// <summary>Player AABB height for noclip testing.</summary>
        private const float PlayerHeight = 1.8f;

        /// <summary>
        ///     Number of consecutive airborne ticks before flight violation triggers.
        ///     Normal jump apex is ~8 ticks; 15 provides margin for stairs/step-up.
        /// </summary>
        private const uint FlightGraceTicks = 15;

        /// <summary>Number of ticks to suppress checks after a teleport confirmation.</summary>
        private const uint PostTeleportGraceTicks = 5;

        /// <summary>Thread-safe chunk data reader for block solidity lookups.</summary>
        private readonly IChunkDataReader _chunkReader;

        /// <summary>Burst-accessible state registry for block collision shape lookups.</summary>
        private readonly NativeStateRegistry _stateRegistry;

        /// <summary>Creates a validator backed by the given chunk data sources.</summary>
        public PlayerMovementValidator(
            IChunkDataReader chunkReader,
            NativeStateRegistry stateRegistry)
        {
            _chunkReader = chunkReader;
            _stateRegistry = stateRegistry;
        }

        /// <summary>
        ///     Validates the claimed position against the last accepted position in
        ///     <paramref name="state" />. Updates <paramref name="state" /> in place.
        ///     Returns the accepted position (claimed if clean, last valid if rejected).
        ///     Sets <paramref name="needsTeleport" /> to true when VL exceeds threshold.
        /// </summary>
        public float3 Validate(
            float3 claimedPosition,
            byte flags,
            ref PlayerValidationState state,
            out bool needsTeleport)
        {
            needsTeleport = false;

            // Grace period after teleport: accept position unconditionally
            if (state.GraceTicks > 0)
            {
                state.GraceTicks--;
                state.LastAcceptedPosition = claimedPosition;
                state.ViolationLevel = math.max(0f, state.ViolationLevel - VlDecayPerTick);

                return claimedPosition;
            }

            bool anyViolation = false;

            // 1. Speed check: squared distance vs threshold
            float3 delta = claimedPosition - state.LastAcceptedPosition;
            float distSq = math.lengthsq(delta);

            if (distSq > MaxDistanceSqPerTick)
            {
                state.ViolationLevel += VlPerSpeedViolation;
                anyViolation = true;
            }

            // 2. Noclip check: verify the claimed AABB does not intersect solid blocks
            if (!anyViolation && IsInsideSolidBlock(claimedPosition))
            {
                state.ViolationLevel += VlPerNoclipViolation;
                anyViolation = true;
            }

            // 3. Flight check: detect sustained airborne movement without jump or fly
            bool groundBelow = HasGroundBelow(claimedPosition);

            if (groundBelow)
            {
                state.AirborneTicks = 0;
            }
            else
            {
                state.AirborneTicks++;
            }

            bool jumping = (flags & InputFlags.Jump) != 0;
            bool flyToggle = (flags & InputFlags.FlyToggle) != 0;
            bool movingUp = claimedPosition.y > state.LastAcceptedPosition.y + 0.01f;

            if (!groundBelow && !jumping && !flyToggle && movingUp
                && state.AirborneTicks > FlightGraceTicks)
            {
                state.ViolationLevel += VlPerFlightViolation;
                anyViolation = true;
            }

            // 4. Decay VL on clean ticks
            if (!anyViolation)
            {
                state.ViolationLevel = math.max(0f, state.ViolationLevel - VlDecayPerTick);
            }

            // 5. Decide: accept or reject
            if (state.ViolationLevel >= VlTeleportThreshold)
            {
                needsTeleport = true;

                return state.LastAcceptedPosition;
            }

            state.LastAcceptedPosition = claimedPosition;

            return claimedPosition;
        }

        /// <summary>
        ///     Resets a player's validation state after a confirmed teleport. Grants
        ///     a grace period to suppress false positives from the position discontinuity.
        /// </summary>
        internal static void OnTeleportConfirmed(ref PlayerValidationState state)
        {
            state.AwaitingTeleportConfirm = false;
            state.ViolationLevel = 0f;
            state.AirborneTicks = 0;
            state.GraceTicks = PostTeleportGraceTicks;
        }

        /// <summary>
        ///     Returns true if the player AABB at <paramref name="position" /> overlaps
        ///     any solid block. Uses a conservative scan of the full AABB bounds.
        ///     Unloaded blocks are treated as non-solid to avoid false positives near
        ///     chunk boundaries (the player may legitimately be in unstreamed terrain).
        /// </summary>
        private bool IsInsideSolidBlock(float3 position)
        {
            int minX = (int)math.floor(position.x - PlayerHalfWidth);
            int minY = (int)math.floor(position.y);
            int minZ = (int)math.floor(position.z - PlayerHalfWidth);
            int maxX = (int)math.floor(position.x + PlayerHalfWidth);
            int maxY = (int)math.floor(position.y + PlayerHeight);
            int maxZ = (int)math.floor(position.z + PlayerHalfWidth);

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    for (int z = minZ; z <= maxZ; z++)
                    {
                        int3 coord = new(x, y, z);

                        if (!_chunkReader.IsBlockLoaded(coord))
                        {
                            continue;
                        }

                        StateId blockState = _chunkReader.GetBlock(coord);

                        if (blockState.Value == 0)
                        {
                            continue;
                        }

                        BlockStateCompact compact = _stateRegistry.States[blockState.Value];

                        if (compact.CollisionShape != 0)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        ///     Returns true if there is a solid block within a thin band below the
        ///     player's feet position. Checks the 4 blocks under the AABB corners
        ///     to handle edge-standing.
        /// </summary>
        private bool HasGroundBelow(float3 position)
        {
            float checkY = position.y - 0.05f;
            int by = (int)math.floor(checkY);

            int minX = (int)math.floor(position.x - PlayerHalfWidth);
            int maxX = (int)math.floor(position.x + PlayerHalfWidth);
            int minZ = (int)math.floor(position.z - PlayerHalfWidth);
            int maxZ = (int)math.floor(position.z + PlayerHalfWidth);

            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    int3 coord = new(x, by, z);

                    if (!_chunkReader.IsBlockLoaded(coord))
                    {
                        continue;
                    }

                    StateId blockState = _chunkReader.GetBlock(coord);

                    if (blockState.Value == 0)
                    {
                        continue;
                    }

                    BlockStateCompact compact = _stateRegistry.States[blockState.Value];

                    if (compact.CollisionShape != 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
