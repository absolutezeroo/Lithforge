using System.Collections.Generic;
using Lithforge.Voxel.Command;
using Unity.Mathematics;


namespace Lithforge.Network.Server
{
    /// <summary>
    /// Server-side per-player chunk interest tracking. Tracks which chunks
    /// the player has received, what needs to be sent next, and the streaming
    /// rate budget. Allocated when a peer transitions to Loading state.
    /// </summary>
    public sealed class PlayerInterestState
    {
        /// <summary>Chunk coordinate of the player's current position.</summary>
        public int3 CurrentChunk;

        /// <summary>Previous tick's chunk coordinate. Used to detect chunk boundary crossings.</summary>
        public int3 PreviousChunk;

        /// <summary>Set of chunk coordinates already sent to this client.</summary>
        public readonly HashSet<int3> LoadedChunks;

        /// <summary>
        /// View radius in chunks (8-16). Determines which chunks are in this player's
        /// interest region. Clamped to the server's max render distance.
        /// </summary>
        public int ViewRadius;

        /// <summary>
        /// Priority queue of chunk coordinates that need to be streamed to this player.
        /// Sorted by distance from current chunk (closest first), with look-direction bias.
        /// </summary>
        public readonly List<int3> StreamingQueue;

        /// <summary>
        /// Cached sort keys for StreamingQueue (Schwartzian transform).
        /// Parallel array. Avoids recomputing keys during sort.
        /// </summary>
        public readonly List<float> StreamingScores;

        /// <summary>Cursor into StreamingQueue. Advanced instead of RemoveRange(0,n).</summary>
        public int StreamingQueueIndex;

        /// <summary>
        /// The most recent MoveInput.SequenceId processed by the server for this player.
        /// Echoed back in PlayerStateMessage so the client can discard acknowledged predictions.
        /// </summary>
        public ushort LastProcessedSequenceId;

        /// <summary>
        /// True during the initial chunk streaming phase (Loading state).
        /// Allows a higher streaming rate (4 chunks/tick vs 2 steady-state).
        /// </summary>
        public bool IsInitialLoad;

        /// <summary>
        /// Per-player command buffer for MoveCommands received from the network.
        /// Server drains the most recent command per tick; excess commands are buffered.
        /// </summary>
        public readonly CommandRingBuffer<MoveCommand> MoveBuffer;

        /// <summary>
        /// Queue of pending PlaceBlock commands for this tick.
        /// Drained in Phase 2 after movement processing.
        /// </summary>
        public readonly List<PlaceBlockCommand> PendingPlaceCommands;

        /// <summary>
        /// Queue of pending BreakBlock commands for this tick.
        /// Drained in Phase 2 after movement processing.
        /// </summary>
        public readonly List<BreakBlockCommand> PendingBreakCommands;

        /// <summary>
        /// Queue of pending StartDigging commands for this tick.
        /// Drained in Phase 2 before break commands.
        /// </summary>
        public readonly List<StartDiggingCommand> PendingStartDiggingCommands;

        /// <summary>
        /// Set of player IDs for which a SpawnPlayerMessage has been sent to this observer.
        /// Used by <see cref="ServerGameLoop.BroadcastPlayerPresenceChanges"/> to detect
        /// first-entry and exit events for remote player spawn/despawn lifecycle.
        /// </summary>
        public readonly HashSet<ushort> SpawnedRemotePlayers;

        /// <summary>
        /// The Flags from the most recently processed MoveInput.
        /// Used as the default input when no MoveInput arrives for a tick (packet loss).
        /// </summary>
        public byte LastKnownInputFlags;

        /// <summary>
        /// The yaw and pitch from the most recently processed MoveInput.
        /// Used for input repeat on missing commands and look-direction bias.
        /// </summary>
        public float2 LastKnownLookDir;

        /// <summary>
        /// The authoritative spawn position computed by the server.
        /// Used by ChunkStreamingManager as the center for initial chunk streaming.
        /// </summary>
        public float3 SpawnPosition;

        /// <summary>
        /// Number of chunks sent to this client that have not yet been acknowledged.
        /// Incremented by <see cref="ChunkStreamingManager" /> on each chunk send,
        /// decremented when a <see cref="ChunkBatchAckMessage" /> arrives.
        /// Streaming pauses when this exceeds <see cref="MaxInFlightChunks" />.
        /// </summary>
        public int UnackedChunks;

        /// <summary>
        /// Maximum number of chunks allowed in-flight before the server pauses streaming
        /// for this peer. Prevents overwhelming slow clients. Default: 32.
        /// </summary>
        public int MaxInFlightChunks;

        /// <summary>
        /// Creates a new PlayerInterestState with the given view radius and default values.
        /// </summary>
        public PlayerInterestState(int viewRadius)
        {
            ViewRadius = viewRadius;
            CurrentChunk = int3.zero;
            PreviousChunk = new int3(int.MinValue, int.MinValue, int.MinValue);
            LoadedChunks = new HashSet<int3>();
            StreamingQueue = new List<int3>();
            StreamingScores = new List<float>();
            StreamingQueueIndex = 0;
            LastProcessedSequenceId = 0;
            IsInitialLoad = true;
            MoveBuffer = new CommandRingBuffer<MoveCommand>();
            PendingPlaceCommands = new List<PlaceBlockCommand>();
            PendingBreakCommands = new List<BreakBlockCommand>();
            PendingStartDiggingCommands = new List<StartDiggingCommand>();
            SpawnedRemotePlayers = new HashSet<ushort>();
            LastKnownInputFlags = 0;
            LastKnownLookDir = float2.zero;
            SpawnPosition = float3.zero;
            UnackedChunks = 0;
            MaxInFlightChunks = NetworkConstants.MaxInFlightChunks;
        }
    }
}
