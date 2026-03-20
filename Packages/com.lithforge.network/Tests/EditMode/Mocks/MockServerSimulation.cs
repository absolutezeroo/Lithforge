using Lithforge.Network.Server;
using Lithforge.Voxel.Command;

using Unity.Mathematics;

namespace Lithforge.Network.Tests.Mocks
{
    /// <summary>Minimal mock of <see cref="IServerSimulation"/> for testing ServerGameLoop tick phases.</summary>
    internal sealed class MockServerSimulation : IServerSimulation
    {
        /// <summary>Number of times TickWorldSystems was called.</summary>
        public int TickWorldSystemsCallCount;

        /// <summary>Set of added player IDs.</summary>
        public HashSet<ushort> AddedPlayers = new();

        /// <summary>Configurable physics state returned by AddPlayer and GetPlayerState.</summary>
        public PlayerPhysicsState DefaultState;

        /// <summary>Creates a player and tracks the ID.</summary>
        public PlayerPhysicsState AddPlayer(NetworkEntityId playerId, float3 spawnPosition)
        {
            AddedPlayers.Add(playerId.Value);
            return DefaultState;
        }

        /// <summary>Removes a player from tracking.</summary>
        public void RemovePlayer(NetworkEntityId playerId)
        {
            AddedPlayers.Remove(playerId.Value);
        }

        /// <summary>Returns the default state (no actual physics simulation).</summary>
        public PlayerPhysicsState ApplyMoveInput(NetworkEntityId playerId, float yaw, float pitch, byte flags, float tickDt)
        {
            return DefaultState;
        }

        /// <summary>Increments the call counter.</summary>
        public void TickWorldSystems(float tickDt)
        {
            TickWorldSystemsCallCount++;
        }

        /// <summary>Returns the default physics state.</summary>
        public PlayerPhysicsState GetPlayerState(NetworkEntityId playerId)
        {
            return DefaultState;
        }

        /// <summary>Returns 0.5 (noon) as the time of day.</summary>
        public float GetTimeOfDay()
        {
            return 0.5f;
        }
    }
}
