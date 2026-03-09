using System;
using System.Collections.Generic;
using Lithforge.Voxel.Block;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Lithforge.Voxel.Chunk
{
    /// <summary>
    /// Pre-allocates NativeArrays for chunks to avoid per-chunk Allocator.Persistent calls.
    /// Chunks are checked out when needed and returned when unloaded.
    ///
    /// Owner: ChunkPool owns all NativeArrays (both pooled and checked-out).
    /// Dispose: at shutdown, disposes all arrays including any still checked out.
    /// </summary>
    public sealed class ChunkPool : IDisposable
    {
        private readonly Stack<NativeArray<StateId>> _available;
        private readonly List<NativeArray<StateId>> _checkedOut;
        private int _totalAllocated;
        private bool _disposed;

        public int AvailableCount
        {
            get { return _available.Count; }
        }

        public int CheckedOutCount
        {
            get { return _checkedOut.Count; }
        }

        public int TotalAllocated
        {
            get { return _totalAllocated; }
        }

        public ChunkPool(int initialCapacity)
        {
            _available = new Stack<NativeArray<StateId>>(initialCapacity);
            _checkedOut = new List<NativeArray<StateId>>();

            for (int i = 0; i < initialCapacity; i++)
            {
                NativeArray<StateId> array = new NativeArray<StateId>(
                    ChunkConstants.Volume, Allocator.Persistent, NativeArrayOptions.ClearMemory);

                _available.Push(array);
            }

            _totalAllocated = initialCapacity;
        }

        /// <summary>
        /// Checks out a NativeArray from the pool. If the pool is exhausted,
        /// allocates a new one (this is a performance warning — pool should be sized correctly).
        /// The returned array is cleared to all zeros (StateId.Air).
        /// </summary>
        public NativeArray<StateId> Checkout()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ChunkPool));
            }

            NativeArray<StateId> array;

            if (_available.Count > 0)
            {
                array = _available.Pop();
            }
            else
            {
                // Pool exhausted — allocate new
                _totalAllocated++;
                array = new NativeArray<StateId>(
                    ChunkConstants.Volume, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            }

            _checkedOut.Add(array);

            return array;
        }

        /// <summary>
        /// Returns a NativeArray to the pool. The array is cleared before being pooled.
        /// </summary>
        public void Return(NativeArray<StateId> array)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ChunkPool));
            }

            if (!array.IsCreated)
            {
                throw new ArgumentException("Cannot return a disposed NativeArray to the pool.");
            }

            if (array.Length != ChunkConstants.Volume)
            {
                throw new ArgumentException(
                    $"Array length {array.Length} does not match ChunkConstants.Volume ({ChunkConstants.Volume}).");
            }

            _checkedOut.Remove(array);

            // Clear the array using efficient memset (StateId.Air.Value == 0)
            unsafe
            {
                UnsafeUtility.MemClear(
                    NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(array),
                    (long)array.Length * UnsafeUtility.SizeOf<StateId>());
            }

            _available.Push(array);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // Dispose pooled arrays
            while (_available.Count > 0)
            {
                NativeArray<StateId> array = _available.Pop();

                if (array.IsCreated)
                {
                    array.Dispose();
                }
            }

            // Dispose any arrays that are still checked out
            foreach (NativeArray<StateId> array in _checkedOut)
            {
                if (array.IsCreated)
                {
                    array.Dispose();
                }
            }

            _checkedOut.Clear();
        }
    }
}
