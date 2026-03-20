using System.Collections.Generic;

using Lithforge.Network.Server;
using Lithforge.Voxel.Command;

using Unity.Mathematics;

namespace Lithforge.Network.Bridge
{
    /// <summary>
    ///     <see cref="IServerSimulation" /> and <see cref="IPostInputHook" /> implementation
    ///     consumed by <see cref="ServerGameLoop" /> on the server thread. ApplyMoveInput is
    ///     dispatched directly to the real simulation on the server thread — no queue, no
    ///     semaphore. AddPlayer and RemovePlayer use the bridge round-trip because they are
    ///     infrequent lifecycle events that must synchronize with main-thread spawn systems.
    ///     TickWorldSystems continues to use the bridge because it touches Unity job handles
    ///     and managed state on the main thread.
    /// </summary>
    internal sealed class BridgedSimulation : IServerSimulation, IPostInputHook
    {
        /// <summary>Shared cross-thread state for lifecycle round-trips and world tick.</summary>
        private readonly ServerThreadBridge _bridge;

        /// <summary>
        ///     Direct reference to the real server simulation. ApplyMoveInput is called on
        ///     this directly from the server thread, bypassing the bridge semaphore entirely.
        /// </summary>
        private readonly IServerSimulation _directSimulation;

        /// <summary>Cached physics results keyed by player ID value, updated by ApplyMoveInput.</summary>
        private readonly Dictionary<ushort, PlayerPhysicsState> _resultCache = new();

        /// <summary>Creates a bridged simulation with direct physics dispatch.</summary>
        internal BridgedSimulation(ServerThreadBridge bridge, IServerSimulation directSimulation)
        {
            _bridge = bridge;
            _directSimulation = directSimulation;
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
        ///     Calls the real simulation directly on the server thread. No queue, no semaphore.
        ///     Updates the result cache immediately so <see cref="GetPlayerState" /> returns
        ///     current data.
        /// </summary>
        public PlayerPhysicsState ApplyMoveInput(
            NetworkEntityId playerId,
            float yaw,
            float pitch,
            byte flags,
            float tickDt)
        {
            PlayerPhysicsState state = _directSimulation.ApplyMoveInput(
                playerId, yaw, pitch, flags, tickDt);

            _resultCache[playerId.Value] = state;

            return state;
        }

        /// <summary>
        ///     No-op. ApplyMoveInput now runs synchronously on the server thread,
        ///     so no batch signaling or semaphore wait is needed.
        /// </summary>
        public void AfterProcessPlayerInputs()
        {
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
        ///     Returns the cached physics state for the given player from the most recent
        ///     <see cref="ApplyMoveInput" /> call.
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

        /// <summary>
        ///     Returns a copy of the result cache containing all current player physics states.
        ///     Called from the server thread; the cache is populated by <see cref="ApplyMoveInput" />.
        /// </summary>
        public Dictionary<ushort, PlayerPhysicsState> GetAllPlayerStates()
        {
            Dictionary<ushort, PlayerPhysicsState> snapshot = new(_resultCache.Count);

            foreach (KeyValuePair<ushort, PlayerPhysicsState> kvp in _resultCache)
            {
                snapshot[kvp.Key] = kvp.Value;
            }

            return snapshot;
        }
    }
}
