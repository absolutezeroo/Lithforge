using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

using Lithforge.Network.Chunk;
using Lithforge.Voxel.Command;

using Unity.Mathematics;

namespace Lithforge.Network.Bridge
{
    /// <summary>
    ///     Shared state between the server thread and the main thread.
    ///     All fields are thread-safe: ConcurrentQueues for data flow, SemaphoreSlim for signaling.
    ///     Owned and disposed by <see cref="ServerThreadRunner" />.
    /// </summary>
    internal sealed class ServerThreadBridge : IDisposable
    {
        /// <summary>Network events polled on main thread, consumed by BridgedTransport on server thread.</summary>
        public readonly ConcurrentQueue<NetworkEventEnvelope> InboundEvents = new();

        /// <summary>Physics results computed on main thread, consumed by BridgedSimulation on server thread.</summary>
        public readonly ConcurrentQueue<PhysicsTickResult> PhysicsResults = new();

        /// <summary>Dirty block change snapshots from main thread, consumed by BridgedDirtyTracker on server thread.</summary>
        public readonly ConcurrentQueue<Dictionary<int3, List<BlockChangeEntry>>> DirtySnapshots = new();

        /// <summary>Block command results from main thread, consumed by BridgedBlockProcessor on server thread.</summary>
        public readonly ConcurrentQueue<BlockCommandResult> BlockCommandResults = new();

        /// <summary>Outbound sends from server thread, flushed to real transport on main thread.</summary>
        public readonly ConcurrentQueue<SendRequest> OutboundSends = new();

        /// <summary>
        ///     Physics lifecycle requests (AddPlayer, RemovePlayer) from server thread,
        ///     executed on main thread. ApplyMove is dispatched directly on the server
        ///     thread by <see cref="BridgedSimulation" /> and does not flow through this queue.
        /// </summary>
        public readonly ConcurrentQueue<PhysicsTickRequest> PhysicsRequests = new();

        /// <summary>World tick requests from server thread, executed on main thread.</summary>
        public readonly ConcurrentQueue<WorldTickRequest> WorldTickRequests = new();

        /// <summary>Block command requests from server thread, executed on main thread.</summary>
        public readonly ConcurrentQueue<BlockCommandRequest> BlockCommandRequests = new();

        /// <summary>Server signals main that a physics batch is ready for execution.</summary>
        public readonly SemaphoreSlim PhysicsRequestsReady = new(0, 1);

        /// <summary>Main signals server that physics results are ready.</summary>
        public readonly SemaphoreSlim PhysicsResultsReady = new(0, 1);

        /// <summary>Server signals main that a world tick is requested.</summary>
        public readonly SemaphoreSlim WorldTickReady = new(0, 1);

        /// <summary>Main signals server that the world tick completed and dirty snapshot is ready.</summary>
        public readonly SemaphoreSlim WorldTickComplete = new(0, 1);

        /// <summary>Server signals main that a block command is ready for execution.</summary>
        public readonly SemaphoreSlim BlockCommandReady = new(0, 1);

        /// <summary>Main signals server that the block command result is ready.</summary>
        public readonly SemaphoreSlim BlockCommandComplete = new(0, 1);

        // ── Server → Main (deferred actions) ─────────────────────────────

        /// <summary>Actions enqueued from the server thread to run on the main thread next frame.</summary>
        public readonly ConcurrentQueue<Action> DeferredMainThreadActions = new();

        // ── Shutdown / fault ─────────────────────────────────────────────

        /// <summary>Set to true to request the server thread to stop.</summary>
        public volatile bool ShutdownRequested;

        /// <summary>If the server thread throws, the exception is stored here for main thread to rethrow.</summary>
        public volatile Exception FaultException;

        /// <summary>
        /// Latest player chunk positions written by the server thread each tick,
        /// read by the main thread each frame for chunk loading and LOD decisions.
        /// The volatile keyword ensures the main thread sees the latest reference.
        /// Reading a slightly stale snapshot is acceptable because chunk loading
        /// is eventually consistent.
        /// </summary>
        private volatile PlayerChunkSnapshot _playerChunkSnapshot = PlayerChunkSnapshot.Empty;

        /// <summary>
        ///     Latest player physics state snapshot written by the server thread each tick.
        ///     Read by the main thread for periodic save and disconnect save captures.
        ///     Volatile reference swap ensures thread-safe reads without locking.
        /// </summary>
        private volatile Dictionary<ushort, PlayerPhysicsState> _playerPhysicsSnapshot = new();

        /// <summary>Time of day (0-1) written by main thread, read by server thread. Stored as raw int bits for atomic access.</summary>
        private int _cachedTimeOfDayBits;

        /// <summary>Gets or sets the cached time of day using atomic int reinterpretation.</summary>
        public float CachedTimeOfDay
        {
            get
            {
                int bits = Volatile.Read(ref _cachedTimeOfDayBits);
                return BitConverter.Int32BitsToSingle(bits);
            }
            set
            {
                int bits = BitConverter.SingleToInt32Bits(value);
                Volatile.Write(ref _cachedTimeOfDayBits, bits);
            }
        }

        /// <summary>Gets the latest player chunk snapshot written by the server thread.</summary>
        public PlayerChunkSnapshot GetPlayerChunkSnapshot()
        {
            return _playerChunkSnapshot;
        }

        /// <summary>Replaces the player chunk snapshot. Called from the server thread each tick.</summary>
        public void SetPlayerChunkSnapshot(PlayerChunkSnapshot snapshot)
        {
            _playerChunkSnapshot = snapshot;
        }

        /// <summary>Gets the latest player physics snapshot written by the server thread.</summary>
        public Dictionary<ushort, PlayerPhysicsState> GetPlayerPhysicsSnapshot()
        {
            return _playerPhysicsSnapshot;
        }

        /// <summary>Replaces the player physics snapshot. Called from the server thread each tick.</summary>
        public void SetPlayerPhysicsSnapshot(Dictionary<ushort, PlayerPhysicsState> snapshot)
        {
            _playerPhysicsSnapshot = snapshot;
        }

        /// <summary>Disposes all semaphores.</summary>
        public void Dispose()
        {
            PhysicsRequestsReady.Dispose();
            PhysicsResultsReady.Dispose();
            WorldTickReady.Dispose();
            WorldTickComplete.Dispose();
            BlockCommandReady.Dispose();
            BlockCommandComplete.Dispose();
        }
    }
}
