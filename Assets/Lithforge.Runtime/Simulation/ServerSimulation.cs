using System;
using System.Collections.Generic;

using Lithforge.Network;
using Lithforge.Network.Server;
using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Tick;
using Lithforge.Voxel.Command;

using Unity.Mathematics;

namespace Lithforge.Runtime.Simulation
{
    /// <summary>
    ///     Tier 3 implementation of <see cref="IServerSimulation" />. Bridges the network
    ///     package's <see cref="ServerGameLoop" /> to the runtime's <see cref="PlayerPhysicsManager" />
    ///     and <see cref="TickRegistry" />. Reconstructs <see cref="InputSnapshot" /> from
    ///     wire-format input flags for authoritative physics simulation.
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

        /// <summary>Creates a new server simulation with the given runtime dependencies.</summary>
        public ServerSimulation(
            PlayerPhysicsManager playerPhysicsManager,
            TickRegistry tickRegistry,
            PhysicsSettings physicsSettings,
            ServerBlockProcessor blockProcessor,
            Func<float> timeOfDayProvider = null)
        {
            _playerPhysicsManager = playerPhysicsManager;
            _tickRegistry = tickRegistry;
            _physicsSettings = physicsSettings;
            _blockProcessor = blockProcessor;
            _timeOfDayProvider = timeOfDayProvider ?? (() => 0f);
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

        /// <summary>Reconstructs an InputSnapshot from wire-format flags and ticks the player's physics.</summary>
        public PlayerPhysicsState ApplyMoveInput(
            NetworkEntityId playerId, float yaw, float pitch, byte flags, float tickDt)
        {
            // Reconstruct InputSnapshot from wire-format flags.
            // Bits 0-5 are held input, bits 6-7 are edge-triggered toggles.
            InputSnapshot snapshot = new()
            {
                Yaw = yaw,
                Pitch = pitch,
                MoveForward = (flags & InputFlags.MoveForward) != 0,
                MoveBack = (flags & InputFlags.MoveBack) != 0,
                MoveLeft = (flags & InputFlags.MoveLeft) != 0,
                MoveRight = (flags & InputFlags.MoveRight) != 0,
                Sprint = (flags & InputFlags.Sprint) != 0,
                JumpPressed = (flags & InputFlags.Jump) != 0,
                JumpHeld = (flags & InputFlags.Jump) != 0,
                FlyTogglePressed = (flags & InputFlags.FlyToggle) != 0,
                NoclipTogglePressed = (flags & InputFlags.NoclipToggle) != 0,
            };

            _playerPhysicsManager.TickPlayer(playerId, tickDt, in snapshot);
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

        /// <summary>Returns a snapshot of all current player physics states.</summary>
        public Dictionary<ushort, PlayerPhysicsState> GetAllPlayerStates()
        {
            return _playerPhysicsManager.GetAllStates();
        }
    }
}
