using System.Collections.Generic;

using Lithforge.Network.Server;
using Lithforge.Voxel.Command;

using Unity.Mathematics;

namespace Lithforge.Network.Bridge
{
    /// <summary>
    ///     <see cref="IServerSimulation" /> and <see cref="IPostInputHook" /> implementation
    ///     consumed by <see cref="ServerGameLoop" /> on the server thread. Marshals all
    ///     simulation calls to the main thread via the bridge queues and semaphores.
    /// </summary>
    internal sealed class BridgedSimulation : IServerSimulation, IPostInputHook
    {
        /// <summary>Shared cross-thread state.</summary>
        private readonly ServerThreadBridge _bridge;

        /// <summary>Cached physics results from the previous tick, keyed by player ID value.</summary>
        private readonly Dictionary<ushort, PlayerPhysicsState> _resultCache = new();

        /// <summary>Creates a bridged simulation backed by the given shared bridge.</summary>
        public BridgedSimulation(ServerThreadBridge bridge)
        {
            _bridge = bridge;
        }

        /// <summary>
        ///     Enqueues an AddPlayer request and performs a synchronous round-trip to the main thread.
        ///     Returns the initial physics state.
        /// </summary>
        public PlayerPhysicsState AddPlayer(NetworkEntityId playerId, float3 spawnPosition)
        {
            _bridge.PhysicsRequests.Enqueue(new PhysicsTickRequest
            {
                Kind = PhysicsRequestKind.AddPlayer,
                PlayerId = playerId,
                SpawnPosition = spawnPosition,
            });

            // Signal main thread and wait for result
            _bridge.PhysicsRequestsReady.Release();
            _bridge.PhysicsResultsReady.Wait();

            // Drain the single result
            if (_bridge.PhysicsResults.TryDequeue(out PhysicsTickResult result))
            {
                _resultCache[playerId.Value] = result.State;
                return result.State;
            }

            return default;
        }

        /// <summary>
        ///     Enqueues a RemovePlayer request and performs a synchronous round-trip.
        /// </summary>
        public void RemovePlayer(NetworkEntityId playerId)
        {
            _bridge.PhysicsRequests.Enqueue(new PhysicsTickRequest
            {
                Kind = PhysicsRequestKind.RemovePlayer,
                PlayerId = playerId,
            });

            // Signal main thread and wait for completion
            _bridge.PhysicsRequestsReady.Release();
            _bridge.PhysicsResultsReady.Wait();

            // Drain the placeholder result
            _bridge.PhysicsResults.TryDequeue(out PhysicsTickResult _);
            _resultCache.Remove(playerId.Value);
        }

        /// <summary>
        ///     Enqueues a move input for the given player. Returns the previous tick's cached state
        ///     (optimistic). Actual results arrive in <see cref="AfterProcessPlayerInputs" />.
        /// </summary>
        public PlayerPhysicsState ApplyMoveInput(
            NetworkEntityId playerId,
            float yaw,
            float pitch,
            byte flags,
            float tickDt)
        {
            _bridge.PhysicsRequests.Enqueue(new PhysicsTickRequest
            {
                Kind = PhysicsRequestKind.ApplyMove,
                PlayerId = playerId,
                Yaw = yaw,
                Pitch = pitch,
                Flags = flags,
                TickDt = tickDt,
            });

            // Return cached state from previous tick (optimistic)
            if (_resultCache.TryGetValue(playerId.Value, out PlayerPhysicsState cached))
            {
                return cached;
            }

            return default;
        }

        /// <summary>
        ///     Called after all player inputs have been enqueued. Signals the main thread
        ///     to execute the physics batch, then waits for results and updates the cache.
        /// </summary>
        public void AfterProcessPlayerInputs()
        {
            // Signal main thread that the physics batch is ready
            _bridge.PhysicsRequestsReady.Release();

            // Wait for main thread to complete physics and return results
            _bridge.PhysicsResultsReady.Wait();

            // Drain all results into cache
            while (_bridge.PhysicsResults.TryDequeue(out PhysicsTickResult result))
            {
                _resultCache[result.PlayerId.Value] = result.State;
            }
        }

        /// <summary>
        ///     Enqueues a world tick request and waits for the main thread to complete it.
        /// </summary>
        public void TickWorldSystems(float tickDt)
        {
            _bridge.WorldTickRequests.Enqueue(new WorldTickRequest { TickDt = tickDt });

            // Signal main thread and wait for completion
            _bridge.WorldTickReady.Release();
            _bridge.WorldTickComplete.Wait();
        }

        /// <summary>
        ///     Returns the cached physics state for the given player from the last completed tick.
        /// </summary>
        public PlayerPhysicsState GetPlayerState(NetworkEntityId playerId)
        {
            if (_resultCache.TryGetValue(playerId.Value, out PlayerPhysicsState cached))
            {
                return cached;
            }

            return default;
        }

        /// <summary>
        ///     Returns the cached time of day from the main thread (volatile read).
        /// </summary>
        public float GetTimeOfDay()
        {
            return _bridge.CachedTimeOfDay;
        }
    }
}
