using System;

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
        private readonly ServerBlockProcessor _blockProcessor;
        private readonly PhysicsSettings _physicsSettings;
        private readonly PlayerPhysicsManager _playerPhysicsManager;
        private readonly TickRegistry _tickRegistry;
        private readonly Func<float> _timeOfDayProvider;

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

        public PlayerPhysicsState AddPlayer(ushort playerId, float3 spawnPosition)
        {
            PlayerPhysicsBody body = _playerPhysicsManager.AddPlayer(
                playerId, spawnPosition, _physicsSettings);
            body.SpawnReady = true;
            _blockProcessor?.AddPlayer(playerId);
            return body.GetState();
        }

        public void RemovePlayer(ushort playerId)
        {
            _blockProcessor?.RemovePlayer(playerId);
            _playerPhysicsManager.RemovePlayer(playerId);
        }

        public PlayerPhysicsState ApplyMoveInput(
            ushort playerId, float yaw, float pitch, byte flags, float tickDt)
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

        public void TickWorldSystems(float tickDt)
        {
            _tickRegistry.TickAll(tickDt);
        }

        public PlayerPhysicsState GetPlayerState(ushort playerId)
        {
            return _playerPhysicsManager.GetState(playerId);
        }

        public float GetTimeOfDay()
        {
            return _timeOfDayProvider();
        }
    }
}
