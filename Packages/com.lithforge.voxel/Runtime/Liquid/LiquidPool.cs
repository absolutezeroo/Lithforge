using System;
using System.Collections.Generic;
using Lithforge.Voxel.Chunk;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Lithforge.Voxel.Liquid
{
    /// <summary>
    /// Pool for liquid data NativeArrays (32768 bytes per chunk).
    /// Avoids per-chunk Allocator.Persistent calls. Mirrors <see cref="ChunkPool"/> pattern.
    ///
    /// Owner: LiquidPool owns all NativeArrays (both pooled and checked-out).
    /// Dispose: at session shutdown, disposes all arrays including any still checked out.
    /// LiquidData arrays are cleared to 0 (empty liquid) when returned.
    /// </summary>
    public sealed class LiquidPool : IDisposable
    {
        private readonly Stack<NativeArray<byte>> _available;
        private readonly HashSet<NativeArray<byte>> _checkedOut;
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

        public LiquidPool(int initialCapacity)
        {
            _available = new Stack<NativeArray<byte>>(initialCapacity);
            _checkedOut = new HashSet<NativeArray<byte>>();

            for (int i = 0; i < initialCapacity; i++)
            {
                NativeArray<byte> array = new NativeArray<byte>(
                    ChunkConstants.Volume, Allocator.Persistent, NativeArrayOptions.ClearMemory);

                _available.Push(array);
            }

            _totalAllocated = initialCapacity;
        }

        /// <summary>
        /// Checks out a NativeArray from the pool. If the pool is exhausted,
        /// allocates a new one. The returned array is cleared to all zeros (empty liquid).
        /// </summary>
        public NativeArray<byte> Checkout()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(LiquidPool));
            }

            NativeArray<byte> array;

            if (_available.Count > 0)
            {
                array = _available.Pop();
            }
            else
            {
                _totalAllocated++;
                array = new NativeArray<byte>(
                    ChunkConstants.Volume, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            }

            _checkedOut.Add(array);

            return array;
        }

        /// <summary>
        /// Returns a NativeArray to the pool. The array is cleared before being pooled.
        /// </summary>
        public void Return(NativeArray<byte> array)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(LiquidPool));
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

            unsafe
            {
                UnsafeUtility.MemClear(
                    NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(array),
                    (long)array.Length);
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

            while (_available.Count > 0)
            {
                NativeArray<byte> array = _available.Pop();

                if (array.IsCreated)
                {
                    array.Dispose();
                }
            }

            foreach (NativeArray<byte> array in _checkedOut)
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
