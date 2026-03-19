using System.Collections.Generic;

namespace Lithforge.Voxel.Command
{
    /// <summary>
    /// Fixed-size circular buffer indexed by server tick for storing commands.
    /// Used for client-side prediction storage and server-side per-player command queues.
    /// Drop-oldest on overflow (silent overwrite). Single-threaded, main-thread use only.
    /// </summary>
    public sealed class CommandRingBuffer<T> where T : struct
    {
        private readonly T[] _commands;
        private readonly uint[] _ticks;

        /// <summary>
        /// Creates a new ring buffer with the specified capacity.
        /// Capacity should be a power of 2 for fast modulo operations.
        /// Default: 256 ticks (8.5 seconds at 30 TPS).
        /// </summary>
        public CommandRingBuffer(int capacity = 256)
        {
            Capacity = capacity;
            _commands = new T[capacity];
            _ticks = new uint[capacity];
        }

        /// <summary>
        /// Stores a command at the given tick. Silently overwrites any older entry
        /// in the same slot (drop-oldest behavior).
        /// </summary>
        public void Add(uint tick, T command)
        {
            int slot = (int)(tick % (uint)Capacity);
            _ticks[slot] = tick;
            _commands[slot] = command;
        }

        /// <summary>
        /// Attempts to retrieve the command stored at the given tick.
        /// Returns false if the slot does not contain an entry for that exact tick
        /// (either never written or overwritten by a newer entry).
        /// </summary>
        public bool TryGet(uint tick, out T command)
        {
            int slot = (int)(tick % (uint)Capacity);

            if (_ticks[slot] == tick)
            {
                command = _commands[slot];
                return true;
            }

            command = default;
            return false;
        }

        /// <summary>
        /// Fills the caller-provided list with all commands in the tick range
        /// [fromTick, toTick] inclusive. Follows the fill pattern: caller owns
        /// the list, callee clears and adds.
        /// </summary>
        public void GetRange(uint fromTick, uint toTick, List<T> results)
        {
            results.Clear();

            for (uint tick = fromTick; tick <= toTick; tick++)
            {
                if (TryGet(tick, out T command))
                {
                    results.Add(command);
                }
            }
        }

        /// <summary>
        /// Invalidates all entries older than the given tick by zeroing their
        /// tick presence values. O(capacity) worst case but called infrequently
        /// (once per server acknowledgement).
        /// </summary>
        public void DiscardBefore(uint tick)
        {
            for (int i = 0; i < Capacity; i++)
            {
                if (_ticks[i] < tick)
                {
                    _ticks[i] = 0;
                }
            }
        }

        /// <summary>Number of slots in the ring buffer.</summary>
        public int Capacity { get; }
    }
}
