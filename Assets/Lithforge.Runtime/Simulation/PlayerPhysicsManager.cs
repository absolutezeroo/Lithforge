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
    ///     Manages per-player physics bodies. Each side (server and client) owns its own
    ///     instance. In singleplayer there is exactly one body per instance. In multiplayer,
    ///     the server instance holds all connected players; each client holds only its own.
    /// </summary>
    public sealed class PlayerPhysicsManager
    {
        /// <summary>Maps player IDs to their physics bodies.</summary>
        private readonly Dictionary<ushort, PlayerPhysicsBody> _bodies = new();

        /// <summary>Thread-safe chunk data reader for block collision queries.</summary>
        private readonly IChunkDataReader _chunkDataReader;

        /// <summary>Burst-accessible state registry for block collision shape lookups.</summary>
        private readonly NativeStateRegistry _nativeStateRegistry;

        /// <summary>Creates a new player physics manager backed by the given chunk reader and state data.</summary>
        public PlayerPhysicsManager(
            IChunkDataReader chunkDataReader,
            NativeStateRegistry nativeStateRegistry)
        {
            _chunkDataReader = chunkDataReader;
            _nativeStateRegistry = nativeStateRegistry;
        }

        /// <summary>Gets the feet-position of the player with the given ID, or zero if not found.</summary>
        public float3 GetPosition(ushort playerId)
        {
            if (_bodies.TryGetValue(playerId, out PlayerPhysicsBody body))
            {
                return body.CurrentPosition;
            }

            return float3.zero;
        }

        /// <summary>Gets the yaw rotation in degrees for the given player, or 0 if not found.</summary>
        public float GetYaw(ushort playerId)
        {
            if (_bodies.TryGetValue(playerId, out PlayerPhysicsBody body))
            {
                return body.GetState().Yaw;
            }

            return 0f;
        }

        /// <summary>Returns true if the player exists and their spawn sequence is complete.</summary>
        public bool IsReady(ushort playerId)
        {
            if (_bodies.TryGetValue(playerId, out PlayerPhysicsBody body))
            {
                return body.SpawnReady;
            }

            return false;
        }

        /// <summary>Returns true if the given player is currently in fly mode.</summary>
        public bool IsFlying(ushort playerId)
        {
            if (_bodies.TryGetValue(playerId, out PlayerPhysicsBody body))
            {
                return body.IsFlying;
            }

            return false;
        }

        /// <summary>
        ///     Creates a new physics body for the given player and registers it.
        ///     Returns the created body for direct access (e.g. wiring to PlayerController).
        /// </summary>
        public PlayerPhysicsBody AddPlayer(
            ushort playerId, float3 spawnPosition, PhysicsSettings settings)
        {
            PlayerPhysicsBody body = new(
                spawnPosition, _chunkDataReader, _nativeStateRegistry, settings);

            _bodies[playerId] = body;
            return body;
        }

        /// <summary>
        ///     Removes the physics body for the given player.
        /// </summary>
        public void RemovePlayer(ushort playerId)
        {
            _bodies.Remove(playerId);
        }

        /// <summary>
        ///     Ticks a single player's physics with the given input snapshot.
        ///     Used by the client (local player only) and singleplayer.
        /// </summary>
        public void TickPlayer(ushort playerId, float tickDt, in InputSnapshot snapshot)
        {
            if (_bodies.TryGetValue(playerId, out PlayerPhysicsBody body))
            {
                body.TickWithSnapshot(tickDt, in snapshot);
            }
        }

        /// <summary>
        ///     Ticks all players' physics. Used by the server to simulate all connected players.
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
        ///     Returns the blittable physics state for the given player.
        ///     Returns default if the player does not exist.
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
        ///     Returns the physics body for direct access (interpolation, benchmark, etc.).
        ///     Returns null if the player does not exist.
        /// </summary>
        public PlayerPhysicsBody GetBody(ushort playerId)
        {
            _bodies.TryGetValue(playerId, out PlayerPhysicsBody body);
            return body;
        }

        /// <summary>
        ///     Returns a snapshot of all current player physics states keyed by player ID.
        ///     Used by the save system to capture positions for all connected players.
        /// </summary>
        public Dictionary<ushort, PlayerPhysicsState> GetAllStates()
        {
            Dictionary<ushort, PlayerPhysicsState> result = new(_bodies.Count);

            foreach (KeyValuePair<ushort, PlayerPhysicsBody> kvp in _bodies)
            {
                result[kvp.Key] = kvp.Value.GetState();
            }

            return result;
        }
    }
}
