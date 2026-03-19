using System;
using System.Collections.Generic;

using Lithforge.Network.Chunk;
using Lithforge.Network.Server;
using Lithforge.Network.Transport;

using Unity.Mathematics;

namespace Lithforge.Network.Bridge
{
    /// <summary>
    ///     Called each frame on the main thread by <c>GameLoopPoco.Update()</c>.
    ///     Pumps the real transport, services physics/block/world-tick requests from
    ///     the server thread, and flushes outbound sends.
    /// </summary>
    public sealed class MainThreadBridgePump
    {
        /// <summary>Shared cross-thread state.</summary>
        private readonly ServerThreadBridge _bridge;

        /// <summary>The real transport that talks to UTP/DirectTransport on the main thread.</summary>
        private readonly INetworkTransport _realTransport;

        /// <summary>The real simulation running on the main thread (player physics, world tick).</summary>
        private readonly IServerSimulation _realSimulation;

        /// <summary>The real block processor running on the main thread.</summary>
        private readonly IServerBlockProcessor _realBlockProcessor;

        /// <summary>The real dirty tracker running on the main thread.</summary>
        private readonly ChunkDirtyTracker _realDirtyTracker;

        /// <summary>Callback to get current time of day from the main thread.</summary>
        private readonly Func<float> _timeOfDayProvider;

        /// <summary>Creates a new pump wiring the bridge to the real main-thread implementations.</summary>
        internal MainThreadBridgePump(
            ServerThreadBridge bridge,
            INetworkTransport realTransport,
            IServerSimulation realSimulation,
            IServerBlockProcessor realBlockProcessor,
            ChunkDirtyTracker realDirtyTracker,
            Func<float> timeOfDayProvider)
        {
            _bridge = bridge;
            _realTransport = realTransport;
            _realSimulation = realSimulation;
            _realBlockProcessor = realBlockProcessor;
            _realDirtyTracker = realDirtyTracker;
            _timeOfDayProvider = timeOfDayProvider;
        }

        /// <summary>
        ///     Enqueues an action to be executed on the main thread during the next Tick().
        ///     Thread-safe: can be called from the server thread.
        /// </summary>
        public void EnqueueMainThreadAction(Action action)
        {
            _bridge.DeferredMainThreadActions.Enqueue(action);
        }

        /// <summary>
        ///     Executes one frame of bridge work on the main thread.
        ///     Must be called from <c>GameLoopPoco.Update()</c>.
        /// </summary>
        public void Tick()
        {
            // 1. Update cached time of day for server thread reads
            _bridge.CachedTimeOfDay = _timeOfDayProvider();

            // 2. Pump inbound network events → bridge queue
            PumpInbound();

            // 3. Service physics requests (non-blocking check)
            ServicePhysicsRequests();

            // 4. Service block commands (non-blocking check, may be called multiple times per frame)
            ServiceBlockCommands();

            // 5. Service world tick (non-blocking check)
            ServiceWorldTick();

            // 6. Flush outbound sends to real transport
            FlushOutbound();

            // 7. Execute deferred main-thread actions enqueued from the server thread
            DrainDeferredActions();
        }

        /// <summary>
        ///     Pumps the real transport and copies all events into the bridge inbound queue.
        /// </summary>
        private void PumpInbound()
        {
            _realTransport.Update();

            while (true)
            {
                NetworkEventType eventType = _realTransport.PollEvent(
                    out ConnectionId connectionId,
                    out byte[] data,
                    out int offset,
                    out int length);

                if (eventType == NetworkEventType.Empty)
                {
                    break;
                }

                // Defensive copy: transport may reuse the buffer
                byte[] copy = null;

                if (data is not null && length > 0)
                {
                    copy = new byte[length];
                    Buffer.BlockCopy(data, offset, copy, 0, length);
                }

                _bridge.InboundEvents.Enqueue(new NetworkEventEnvelope
                {
                    EventType = eventType,
                    ConnectionId = connectionId,
                    Data = copy,
                    Length = length,
                });
            }
        }

        /// <summary>
        ///     Non-blocking check for physics requests. If the server thread has signaled,
        ///     drains and executes all physics requests, enqueues results, and signals back.
        /// </summary>
        private void ServicePhysicsRequests()
        {
            if (!_bridge.PhysicsRequestsReady.Wait(0))
            {
                return;
            }

            while (_bridge.PhysicsRequests.TryDequeue(out PhysicsTickRequest request))
            {
                PhysicsTickResult result = new() { PlayerId = request.PlayerId };

                switch (request.Kind)
                {
                    case PhysicsRequestKind.ApplyMove:
                        result.State = _realSimulation.ApplyMoveInput(
                            request.PlayerId, request.Yaw, request.Pitch,
                            request.Flags, request.TickDt);
                        break;

                    case PhysicsRequestKind.AddPlayer:
                        result.State = _realSimulation.AddPlayer(
                            request.PlayerId, request.SpawnPosition);
                        break;

                    case PhysicsRequestKind.RemovePlayer:
                        _realSimulation.RemovePlayer(request.PlayerId);
                        break;
                }

                _bridge.PhysicsResults.Enqueue(result);
            }

            _bridge.PhysicsResultsReady.Release();
        }

        /// <summary>
        ///     Non-blocking check for block commands. Services all pending block commands
        ///     one at a time (each is a synchronous round-trip from the server thread's perspective).
        /// </summary>
        private void ServiceBlockCommands()
        {
            // Service all pending block commands this frame
            while (_bridge.BlockCommandReady.Wait(0))
            {
                if (!_bridge.BlockCommandRequests.TryDequeue(out BlockCommandRequest request))
                {
                    continue;
                }

                BlockCommandResult result = new();

                switch (request.Kind)
                {
                    case BlockCommandKind.TryBreakBlock:
                        result.ProcessResult = _realBlockProcessor.TryBreakBlock(
                            request.PlayerId, request.Position,
                            request.PlayerPosition, request.ServerTick);
                        break;

                    case BlockCommandKind.TryPlaceBlock:
                        result.ProcessResult = _realBlockProcessor.TryPlaceBlock(
                            request.PlayerId, request.Position,
                            request.BlockState, request.Face, request.PlayerPosition);
                        break;

                    case BlockCommandKind.StartDigging:
                        result.StartDiggingResult = _realBlockProcessor.StartDigging(
                            request.PlayerId, request.Position,
                            request.PlayerPosition, request.ServerTick);
                        break;

                    case BlockCommandKind.CancelDigging:
                        _realBlockProcessor.CancelDigging(request.PlayerId);
                        break;

                    case BlockCommandKind.RefillRateLimitTokens:
                        _realBlockProcessor.RefillRateLimitTokens(
                            request.PlayerId, request.CurrentTime);
                        break;

                    case BlockCommandKind.AddPlayer:
                        _realBlockProcessor.AddPlayer(request.PlayerId);
                        break;

                    case BlockCommandKind.RemovePlayer:
                        _realBlockProcessor.RemovePlayer(request.PlayerId);
                        break;

                    case BlockCommandKind.GetBlock:
                        result.GetBlockResult = _realBlockProcessor.GetBlock(request.Position);
                        break;
                }

                _bridge.BlockCommandResults.Enqueue(result);
                _bridge.BlockCommandComplete.Release();
            }
        }

        /// <summary>
        ///     Non-blocking check for world tick requests. If the server thread has signaled,
        ///     runs the world tick, flushes dirty changes into a snapshot, and signals back.
        /// </summary>
        private void ServiceWorldTick()
        {
            if (!_bridge.WorldTickReady.Wait(0))
            {
                return;
            }

            // Drain the request
            _bridge.WorldTickRequests.TryDequeue(out WorldTickRequest request);

            float tickDt = request?.TickDt ?? (1f / 30f);

            // Execute world tick on main thread
            _realSimulation.TickWorldSystems(tickDt);

            // Flush dirty changes and deep-copy into the bridge queue.
            // ChunkDirtyTracker.FlushAll() swaps buffers — the returned dict is only valid
            // until the next FlushAll call. We must copy because the server thread reads it later.
            Dictionary<int3, List<BlockChangeEntry>> source = _realDirtyTracker.FlushAll();
            Dictionary<int3, List<BlockChangeEntry>> snapshot = new(source.Count);

            foreach (KeyValuePair<int3, List<BlockChangeEntry>> pair in source)
            {
                if (pair.Value.Count > 0)
                {
                    List<BlockChangeEntry> copy = new(pair.Value);
                    snapshot[pair.Key] = copy;
                }
            }

            _bridge.DirtySnapshots.Enqueue(snapshot);
            _bridge.WorldTickComplete.Release();
        }

        /// <summary>
        ///     Drains all outbound sends from the bridge and delivers them via the real transport.
        /// </summary>
        private void FlushOutbound()
        {
            while (_bridge.OutboundSends.TryDequeue(out SendRequest send))
            {
                if (send.PipelineId == -1)
                {
                    // Sentinel: disconnect request
                    _realTransport.Disconnect(send.ConnectionId);
                }
                else
                {
                    _realTransport.Send(
                        send.ConnectionId, send.PipelineId,
                        send.Data, send.Offset, send.Length);
                }
            }
        }

        /// <summary>
        ///     Drains and executes all deferred main-thread actions enqueued from the server thread.
        /// </summary>
        private void DrainDeferredActions()
        {
            while (_bridge.DeferredMainThreadActions.TryDequeue(out Action action))
            {
                action();
            }
        }
    }
}
