using System.Collections.Generic;
using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Tick;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Command;
using Unity.Mathematics;

namespace Lithforge.Runtime.Simulation
{
    /// <summary>
    /// Manages per-player physics bodies. In singleplayer there is exactly one player (id=0).
    /// In multiplayer, the server creates a body per connected player and ticks them all.
    /// Implements <see cref="IPlayerManager"/> so command processors can query player state.
    /// </summary>
    public sealed class PlayerPhysicsManager : IPlayerManager
    {
        private readonly Dictionary<ushort, PlayerPhysicsBody> _bodies = new();

        private readonly ChunkManager _chunkManager;
        private readonly NativeStateRegistry _nativeStateRegistry;

        public PlayerPhysicsManager(
            ChunkManager chunkManager,
            NativeStateRegistry nativeStateRegistry)
        {
            _chunkManager = chunkManager;
            _nativeStateRegistry = nativeStateRegistry;
        }

        /// <summary>
        /// Creates a new physics body for the given player and registers it.
        /// Returns the created body for direct access (e.g. wiring to PlayerController).
        /// </summary>
        public PlayerPhysicsBody AddPlayer(
            ushort playerId, float3 spawnPosition, PhysicsSettings settings)
        {
            PlayerPhysicsBody body = new(
                spawnPosition, _chunkManager, _nativeStateRegistry, settings);

            _bodies[playerId] = body;
            return body;
        }

        /// <summary>
        /// Removes the physics body for the given player.
        /// </summary>
        public void RemovePlayer(ushort playerId)
        {
            _bodies.Remove(playerId);
        }

        /// <summary>
        /// Ticks a single player's physics with the given input snapshot.
        /// Used by the client (local player only) and singleplayer.
        /// </summary>
        public void TickPlayer(ushort playerId, float tickDt, in InputSnapshot snapshot)
        {
            if (_bodies.TryGetValue(playerId, out PlayerPhysicsBody body))
            {
                body.TickWithSnapshot(tickDt, in snapshot);
            }
        }

        /// <summary>
        /// Ticks all players' physics. Used by the server to simulate all connected players.
        /// </summary>
        public void TickAll(float tickDt, Dictionary<ushort, InputSnapshot> snapshots)
        {
            foreach (KeyValuePair<ushort, InputSnapshot> pair in snapshots)
            {
                if (_bodies.TryGetValue(pair.Key, out PlayerPhysicsBody body))
                {
                    InputSnapshot snapshot = pair.Value;
                    body.TickWithSnapshot(tickDt, in snapshot);
                }
            }
        }

        /// <summary>
        /// Returns the blittable physics state for the given player.
        /// Returns default if the player does not exist.
        /// </summary>
        public PlayerPhysicsState GetState(ushort playerId)
        {
            if (_bodies.TryGetValue(playerId, out PlayerPhysicsBody body))
            {
                return body.GetState();
            }

            return default;
        }

        /// <summary>
        /// Returns the physics body for direct access (interpolation, benchmark, etc.).
        /// Returns null if the player does not exist.
        /// </summary>
        public PlayerPhysicsBody GetBody(ushort playerId)
        {
            _bodies.TryGetValue(playerId, out PlayerPhysicsBody body);
            return body;
        }

        // ── IPlayerManager implementation ──

        public float3 GetPosition(ushort playerId)
        {
            if (_bodies.TryGetValue(playerId, out PlayerPhysicsBody body))
            {
                return body.CurrentPosition;
            }

            return float3.zero;
        }

        public float GetYaw(ushort playerId)
        {
            if (_bodies.TryGetValue(playerId, out PlayerPhysicsBody body))
            {
                return body.GetState().Yaw;
            }

            return 0f;
        }

        public bool IsReady(ushort playerId)
        {
            if (_bodies.TryGetValue(playerId, out PlayerPhysicsBody body))
            {
                return body.SpawnReady;
            }

            return false;
        }

        public bool IsFlying(ushort playerId)
        {
            if (_bodies.TryGetValue(playerId, out PlayerPhysicsBody body))
            {
                return body.IsFlying;
            }

            return false;
        }
    }
}
