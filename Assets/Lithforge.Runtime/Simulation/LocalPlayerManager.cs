using Lithforge.Runtime.Tick;
using Unity.Mathematics;
using UnityEngine;

namespace Lithforge.Runtime.Simulation
{
    /// <summary>
    /// Singleplayer implementation of <see cref="IPlayerManager"/>.
    /// Wraps the single local player's <see cref="PlayerPhysicsBody"/>
    /// and Transform. Player ID 0 is the local player; all other IDs return defaults.
    /// </summary>
    public sealed class LocalPlayerManager : IPlayerManager
    {
        private const ushort LocalPlayerId = 0;

        private readonly PlayerPhysicsBody _physicsBody;
        private readonly Transform _playerTransform;

        public LocalPlayerManager(PlayerPhysicsBody physicsBody, Transform playerTransform)
        {
            _physicsBody = physicsBody;
            _playerTransform = playerTransform;
        }

        public float3 GetPosition(ushort playerId)
        {
            if (playerId != LocalPlayerId || _physicsBody == null)
            {
                return float3.zero;
            }

            return _physicsBody.CurrentPosition;
        }

        public float GetYaw(ushort playerId)
        {
            if (playerId != LocalPlayerId || _playerTransform == null)
            {
                return 0f;
            }

            return _playerTransform.eulerAngles.y;
        }

        public bool IsReady(ushort playerId)
        {
            if (playerId != LocalPlayerId)
            {
                return false;
            }

            return _physicsBody.SpawnReady;
        }

        public bool IsFlying(ushort playerId)
        {
            if (playerId != LocalPlayerId)
            {
                return false;
            }

            return _physicsBody.IsFlying;
        }
    }
}
