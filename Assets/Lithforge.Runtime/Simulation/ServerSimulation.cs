using System;

using Lithforge.Network;
using Lithforge.Network.Server;
using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Tick;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Command;

using Unity.Mathematics;

namespace Lithforge.Runtime.Simulation
{
    /// <summary>
    ///     Tier 3 implementation of <see cref="IServerSimulation" />. Bridges the network
    ///     package's <see cref="ServerGameLoop" /> to the runtime's <see cref="PlayerPhysicsManager" />
    ///     and <see cref="TickRegistry" />. Validates client-submitted positions via
    ///     <see cref="PlayerMovementValidator" /> and accepts valid positions by teleporting
    ///     the server-side physics body.
    /// </summary>
    public sealed class ServerSimulation : IServerSimulation
    {
        /// <summary>Server-side block command validator and executor.</summary>
        private readonly ServerBlockProcessor _blockProcessor;

        /// <summary>Physics settings used when creating new player bodies.</summary>
        private readonly PhysicsSettings _physicsSettings;

        /// <summary>Manages physics bodies for all connected players.</summary>
        private readonly PlayerPhysicsManager _playerPhysicsManager;

        /// <summary>Registry of all ITickable systems driven each server tick.</summary>
        private readonly TickRegistry _tickRegistry;

        /// <summary>Callback returning the current time-of-day value for state broadcasts.</summary>
        private readonly Func<float> _timeOfDayProvider;

        /// <summary>Validates client-submitted positions against collision geometry and movement rules.</summary>
        private readonly PlayerMovementValidator _validator;

        /// <summary>Creates a new server simulation with the given runtime dependencies.</summary>
        public ServerSimulation(
            PlayerPhysicsManager playerPhysicsManager,
            TickRegistry tickRegistry,
            PhysicsSettings physicsSettings,
            ServerBlockProcessor blockProcessor,
            IChunkDataReader chunkDataReader,
            NativeStateRegistry nativeStateRegistry,
            Func<float> timeOfDayProvider = null)
        {
            _playerPhysicsManager = playerPhysicsManager;
            _tickRegistry = tickRegistry;
            _physicsSettings = physicsSettings;
            _blockProcessor = blockProcessor;
            _timeOfDayProvider = timeOfDayProvider ?? (() => 0f);
            _validator = new PlayerMovementValidator(chunkDataReader, nativeStateRegistry);
        }

        /// <summary>Creates a physics body for a new player and returns their initial state.</summary>
        public PlayerPhysicsState AddPlayer(NetworkEntityId playerId, float3 spawnPosition)
        {
            PlayerPhysicsBody body = _playerPhysicsManager.AddPlayer(
                playerId, spawnPosition, _physicsSettings);
            body.SpawnReady = true;
            _blockProcessor?.AddPlayer(playerId);

            return body.GetState();
        }

        /// <summary>Removes the physics body and block processor state for a disconnecting player.</summary>
        public void RemovePlayer(NetworkEntityId playerId)
        {
            _blockProcessor?.RemovePlayer(playerId);
            _playerPhysicsManager.RemovePlayer(playerId);
        }

        /// <summary>
        ///     Validates the client-submitted position and, if valid, teleports the server-side
        ///     physics body to match. If invalid, returns the last accepted position and signals
        ///     that a teleport correction is needed.
        /// </summary>
        public PlayerPhysicsState ValidateAndAcceptMove(
            NetworkEntityId playerId,
            float3 claimedPosition,
            float yaw,
            float pitch,
            byte flags,
            ref PlayerValidationState validationState,
            out bool needsTeleport)
        {
            float3 acceptedPos = _validator.Validate(
                claimedPosition, flags, ref validationState, out needsTeleport);

            PlayerPhysicsBody body = _playerPhysicsManager.GetBody(playerId);

            if (body is not null)
            {
                body.SetPosition(acceptedPos);
                body.SetVelocity(new float3(0f, 0f, 0f));
                body.SetFlags(flags);
            }

            return _playerPhysicsManager.GetState(playerId);
        }

        /// <summary>Ticks all registered world systems (time-of-day, block entities, etc.).</summary>
        public void TickWorldSystems(float tickDt)
        {
            _tickRegistry.TickAll(tickDt);
        }

        /// <summary>Returns the current physics state for the given player.</summary>
        public PlayerPhysicsState GetPlayerState(NetworkEntityId playerId)
        {
            return _playerPhysicsManager.GetState(playerId);
        }

        /// <summary>Returns the current time-of-day value from the provider callback.</summary>
        public float GetTimeOfDay()
        {
            return _timeOfDayProvider();
        }

        /// <summary>
        ///     Accepts a client-authoritative position without validation. Teleports the
        ///     server-side physics body to match the client's state so that chunk interest
        ///     tracking and block command reach checks use the correct position.
        ///     Used for the local peer in SP/Host mode.
        /// </summary>
        public void AcceptAuthoritativeState(NetworkEntityId playerId, PlayerPhysicsState state)
        {
            PlayerPhysicsBody body = _playerPhysicsManager.GetBody(playerId);

            if (body is null)
            {
                return;
            }

            body.SetPosition(state.Position);
            body.SetVelocity(state.Velocity);
            body.SetFlags(state.Flags);
        }
    }
}
