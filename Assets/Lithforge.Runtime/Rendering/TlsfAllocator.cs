using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Lithforge.Runtime.Rendering
{
    /// <summary>
    ///     Two-Level Segregated Fit allocator over an integer range [0, Capacity).
    ///     Provides O(1) Alloc and Free with near-best-fit block selection and
    ///     immediate physical coalescing. All sizes and offsets are in element units
    ///     (the caller decides element stride). Uses a flat block header array with
    ///     a recycled free-index stack to avoid per-operation heap allocation.
    ///     Owner: BufferArena (two instances per arena: vertex and index).
    ///     Lifetime: matches the owning BufferArena.
    /// </summary>
    internal sealed class TlsfAllocator
    {
        /// <summary>Number of first-level index bits (covers sizes up to 2^30).</summary>
        private const int FlIndexMax = 30;

        /// <summary>Log2 of the number of second-level subdivisions per first-level class.</summary>
        private const int SlIndexLog2 = 3;

        /// <summary>Number of second-level subdivisions per first-level class.</summary>
        private const int SlIndexCount = 1 << SlIndexLog2;

        /// <summary>Minimum block size to avoid degenerate splits. Must be >= 1.</summary>
        private const int MinBlockSize = 4;

        /// <summary>Sentinel index indicating "no block".</summary>
        private const int NullIndex = -1;

        /// <summary>First-level bitmap: bit i is set if any second-level list in FL class i is non-empty.</summary>
        private int _flBitmap;

        /// <summary>Second-level bitmaps: one per FL class. Bit j set if free-list [i,j] is non-empty.</summary>
        private readonly int[] _slBitmaps = new int[FlIndexMax];

        /// <summary>Free-list heads indexed by [fl * SlIndexCount + sl]. Each entry is a block index or NullIndex.</summary>
        private readonly int[] _freeListHeads = new int[FlIndexMax * SlIndexCount];

        /// <summary>Block header storage. Index 0 is unused (sentinel). Real blocks start at 1.</summary>
        private BlockHeader[] _blocks;

        /// <summary>Number of allocated block header entries (including recycled ones).</summary>
        private int _blockCount;

        /// <summary>Stack of recycled block header indices for reuse.</summary>
        private int[] _recycledIndices;

        /// <summary>Number of recycled indices available on the stack.</summary>
        private int _recycledCount;

        /// <summary>Maps block start offset to block header index for O(1) lookup in Free/SizeAt.</summary>
        private readonly Dictionary<int, int> _offsetToBlock = new();

        /// <summary>Total number of elements currently allocated (not free).</summary>
        private int _usedElements;

        /// <summary>Total element capacity of this allocator's address space.</summary>
        public int Capacity { get; private set; }

        /// <summary>Number of elements currently allocated (occupied by live blocks).</summary>
        public int UsedElements
        {
            get { return _usedElements; }
        }

        /// <summary>Number of elements currently free.</summary>
        public int FreeElements
        {
            get { return Capacity - _usedElements; }
        }

        /// <summary>
        ///     Creates a TlsfAllocator managing [0, capacity) in element units.
        ///     The entire range starts as a single free block.
        /// </summary>
        public TlsfAllocator(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");
            }

            Capacity = capacity;

            // Initialize all free-list heads to null
            for (int i = 0; i < _freeListHeads.Length; i++)
            {
                _freeListHeads[i] = NullIndex;
            }

            // Pre-allocate block header storage
            int initialBlockCap = 256;
            _blocks = new BlockHeader[initialBlockCap];
            _blockCount = 1; // index 0 is unused sentinel

            _recycledIndices = new int[64];
            _recycledCount = 0;

            // Insert the entire range as one free block
            int blockIdx = AllocBlockHeader();
            _blocks[blockIdx] = new BlockHeader
            {
                Offset = 0,
                Size = capacity,
                IsFree = true,
                PrevPhysical = NullIndex,
                NextPhysical = NullIndex,
                PrevFree = NullIndex,
                NextFree = NullIndex,
            };

            _offsetToBlock[0] = blockIdx;
            InsertFreeBlock(blockIdx);
            _usedElements = 0;
        }

        /// <summary>
        ///     Allocates a contiguous run of at least <paramref name="size" /> elements.
        ///     Returns the starting offset, or -1 if no fit is available. O(1) amortized.
        /// </summary>
        public int Alloc(int size)
        {
            if (size <= 0)
            {
                return -1;
            }

            // Round up to minimum block size
            int adjustedSize = Math.Max(size, MinBlockSize);

            // Find a suitable free block via the two-level bitmap
            int blockIdx = FindFreeBlock(adjustedSize);

            if (blockIdx == NullIndex)
            {
                return -1;
            }

            // Remove from free list
            RemoveFreeBlock(blockIdx);

            // Split if the remainder is large enough
            int blockSize = _blocks[blockIdx].Size;
            int remainder = blockSize - adjustedSize;

            if (remainder >= MinBlockSize)
            {
                // Create a new block for the remainder
                int splitIdx = AllocBlockHeader();
                int splitOffset = _blocks[blockIdx].Offset + adjustedSize;
                _blocks[splitIdx] = new BlockHeader
                {
                    Offset = splitOffset,
                    Size = remainder,
                    IsFree = true,
                    PrevPhysical = blockIdx,
                    NextPhysical = _blocks[blockIdx].NextPhysical,
                    PrevFree = NullIndex,
                    NextFree = NullIndex,
                };

                // Link the split block's next-physical's prev to the split block
                if (_blocks[splitIdx].NextPhysical != NullIndex)
                {
                    _blocks[_blocks[splitIdx].NextPhysical].PrevPhysical = splitIdx;
                }

                _blocks[blockIdx].NextPhysical = splitIdx;
                _blocks[blockIdx].Size = adjustedSize;

                _offsetToBlock[splitOffset] = splitIdx;
                InsertFreeBlock(splitIdx);
            }

            _blocks[blockIdx].IsFree = false;
            _usedElements += _blocks[blockIdx].Size;
            return _blocks[blockIdx].Offset;
        }

        /// <summary>
        ///     Returns the block at <paramref name="offset" /> to the free pool.
        ///     Coalesces with physically adjacent free blocks. O(1).
        /// </summary>
        public void Free(int offset)
        {
            int blockIdx = FindBlockByOffset(offset);

            if (blockIdx == NullIndex)
            {
                throw new ArgumentException($"No allocated block found at offset {offset}.");
            }

            if (_blocks[blockIdx].IsFree)
            {
                throw new InvalidOperationException($"Block at offset {offset} is already free.");
            }

            _usedElements -= _blocks[blockIdx].Size;
            _blocks[blockIdx].IsFree = true;

            // Coalesce with next physical block if free
            int nextIdx = _blocks[blockIdx].NextPhysical;

            if (nextIdx != NullIndex && _blocks[nextIdx].IsFree)
            {
                RemoveFreeBlock(nextIdx);
                MergeBlockWithNext(blockIdx, nextIdx);
            }

            // Coalesce with previous physical block if free
            int prevIdx = _blocks[blockIdx].PrevPhysical;

            if (prevIdx != NullIndex && _blocks[prevIdx].IsFree)
            {
                RemoveFreeBlock(prevIdx);
                MergeBlockWithNext(prevIdx, blockIdx);
                blockIdx = prevIdx;
            }

            InsertFreeBlock(blockIdx);
        }

        /// <summary>
        ///     Returns the size of the allocated block at the given offset.
        ///     Used by BufferArena to check if in-place reuse is possible.
        /// </summary>
        public int SizeAt(int offset)
        {
            int blockIdx = FindBlockByOffset(offset);

            if (blockIdx == NullIndex)
            {
                throw new ArgumentException($"No block found at offset {offset}.");
            }

            return _blocks[blockIdx].Size;
        }

        /// <summary>
        ///     Grows the allocator's capacity by inserting a new free block covering
        ///     [oldCapacity, newCapacity). The new block is coalesced with the last
        ///     physical block if it is free. Called after the backing GPU buffer has been resized.
        /// </summary>
        public void Grow(int newCapacity)
        {
            if (newCapacity <= Capacity)
            {
                throw new ArgumentException($"New capacity {newCapacity} must exceed current {Capacity}.");
            }

            int oldCapacity = Capacity;
            int growSize = newCapacity - oldCapacity;
            Capacity = newCapacity;

            // Find the last physical block BEFORE creating the new one,
            // otherwise FindLastPhysicalBlock may find the new (unlinked) block
            int lastIdx = FindLastPhysicalBlock();

            // Create a free block for the new range
            int newBlockIdx = AllocBlockHeader();
            _blocks[newBlockIdx] = new BlockHeader
            {
                Offset = oldCapacity,
                Size = growSize,
                IsFree = true,
                PrevPhysical = NullIndex,
                NextPhysical = NullIndex,
                PrevFree = NullIndex,
                NextFree = NullIndex,
            };

            _offsetToBlock[oldCapacity] = newBlockIdx;

            if (lastIdx != NullIndex)
            {
                // The last block's end should be at oldCapacity
                _blocks[newBlockIdx].PrevPhysical = lastIdx;

                if (_blocks[lastIdx].NextPhysical != NullIndex)
                {
                    throw new InvalidOperationException("Last physical block should not have a next block.");
                }

                _blocks[lastIdx].NextPhysical = newBlockIdx;

                // Coalesce with previous if free
                if (_blocks[lastIdx].IsFree)
                {
                    RemoveFreeBlock(lastIdx);
                    MergeBlockWithNext(lastIdx, newBlockIdx);
                    newBlockIdx = lastIdx;
                }
            }

            InsertFreeBlock(newBlockIdx);
        }

        /// <summary>
        ///     Finds a free block that can satisfy an allocation of the given size.
        ///     Uses the two-level bitmap for O(1) lookup.
        /// </summary>
        private int FindFreeBlock(int size)
        {
            MappingSearch(size, out int fl, out int sl);

            // Search for a free block at or above the target (fl, sl) class
            // First, check the current FL class for any SL >= target
            int slBitmap = _slBitmaps[fl] & (~0 << sl);

            if (slBitmap == 0)
            {
                // No fit in this FL class; search higher FL classes
                int flBitmap = _flBitmap & (~0 << (fl + 1));

                if (flBitmap == 0)
                {
                    return NullIndex; // No fit anywhere
                }

                fl = BitOperations.TrailingZeroCount((uint)flBitmap);
                slBitmap = _slBitmaps[fl];

                if (slBitmap == 0)
                {
                    return NullIndex; // Should not happen if bitmaps are consistent
                }
            }

            sl = BitOperations.TrailingZeroCount((uint)slBitmap);

            int headIdx = _freeListHeads[fl * SlIndexCount + sl];
            return headIdx;
        }

        /// <summary>
        ///     Maps a block size to its first-level and second-level indices.
        ///     For allocation (search), this rounds up to find the smallest class that fits.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void MappingSearch(int size, out int fl, out int sl)
        {
            if (size < (1 << SlIndexLog2))
            {
                fl = 0;
                sl = size;
                return;
            }

            int t = FloorLog2(size);
            sl = (size >> (t - SlIndexLog2)) ^ SlIndexCount;
            fl = t;

            // Round up: if there are remaining bits below the SL granularity, bump SL
            int roundMask = (1 << (t - SlIndexLog2)) - 1;

            if ((size & roundMask) != 0)
            {
                sl++;

                if (sl >= SlIndexCount)
                {
                    sl = 0;
                    fl++;
                }
            }
        }

        /// <summary>
        ///     Maps a block size to its first-level and second-level indices for insertion.
        ///     Rounds down (the block fits in this class or a larger one).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void MappingInsert(int size, out int fl, out int sl)
        {
            if (size < (1 << SlIndexLog2))
            {
                fl = 0;
                sl = size;
                return;
            }

            int t = FloorLog2(size);
            sl = (size >> (t - SlIndexLog2)) ^ SlIndexCount;
            fl = t;
        }

        /// <summary>Returns floor(log2(value)) for positive values.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int FloorLog2(int value)
        {
            return 31 - BitOperations.LeadingZeroCount((uint)value);
        }

        /// <summary>Inserts a free block into the appropriate segregated free-list and updates bitmaps.</summary>
        private void InsertFreeBlock(int blockIdx)
        {
            int size = _blocks[blockIdx].Size;
            MappingInsert(size, out int fl, out int sl);

            if (fl >= FlIndexMax || sl >= SlIndexCount)
            {
                return; // Safety guard for extreme sizes
            }

            int listIdx = fl * SlIndexCount + sl;
            int headIdx = _freeListHeads[listIdx];

            _blocks[blockIdx].PrevFree = NullIndex;
            _blocks[blockIdx].NextFree = headIdx;

            if (headIdx != NullIndex)
            {
                _blocks[headIdx].PrevFree = blockIdx;
            }

            _freeListHeads[listIdx] = blockIdx;

            // Update bitmaps
            _slBitmaps[fl] |= 1 << sl;
            _flBitmap |= 1 << fl;
        }

        /// <summary>Removes a free block from its segregated free-list and updates bitmaps if the list becomes empty.</summary>
        private void RemoveFreeBlock(int blockIdx)
        {
            int size = _blocks[blockIdx].Size;
            MappingInsert(size, out int fl, out int sl);

            if (fl >= FlIndexMax || sl >= SlIndexCount)
            {
                return; // Safety guard
            }

            int listIdx = fl * SlIndexCount + sl;

            int prevIdx = _blocks[blockIdx].PrevFree;
            int nextIdx = _blocks[blockIdx].NextFree;

            if (prevIdx != NullIndex)
            {
                _blocks[prevIdx].NextFree = nextIdx;
            }
            else
            {
                _freeListHeads[listIdx] = nextIdx;
            }

            if (nextIdx != NullIndex)
            {
                _blocks[nextIdx].PrevFree = prevIdx;
            }

            _blocks[blockIdx].PrevFree = NullIndex;
            _blocks[blockIdx].NextFree = NullIndex;

            // Update bitmaps if list is now empty
            if (_freeListHeads[listIdx] == NullIndex)
            {
                _slBitmaps[fl] &= ~(1 << sl);

                if (_slBitmaps[fl] == 0)
                {
                    _flBitmap &= ~(1 << fl);
                }
            }
        }

        /// <summary>
        ///     Merges block at <paramref name="firstIdx" /> with its physically-next block at
        ///     <paramref name="secondIdx" />. The second block header is recycled.
        /// </summary>
        private void MergeBlockWithNext(int firstIdx, int secondIdx)
        {
            // Remove the second block's offset mapping before merging
            _offsetToBlock.Remove(_blocks[secondIdx].Offset);

            _blocks[firstIdx].Size += _blocks[secondIdx].Size;
            _blocks[firstIdx].NextPhysical = _blocks[secondIdx].NextPhysical;

            int nextNext = _blocks[secondIdx].NextPhysical;

            if (nextNext != NullIndex)
            {
                _blocks[nextNext].PrevPhysical = firstIdx;
            }

            // Recycle the second block header
            RecycleBlockHeader(secondIdx);
        }

        /// <summary>
        ///     Finds the block header index for the block starting at <paramref name="offset" />.
        ///     Returns NullIndex if not found. O(1) via dictionary lookup.
        /// </summary>
        private int FindBlockByOffset(int offset)
        {
            if (_offsetToBlock.TryGetValue(offset, out int blockIdx))
            {
                return blockIdx;
            }

            return NullIndex;
        }

        /// <summary>
        ///     Finds the last physical block by walking the chain from any block that
        ///     ends at or near the old capacity boundary. Used only during Grow.
        /// </summary>
        private int FindLastPhysicalBlock()
        {
            // Walk from any known block to find one whose NextPhysical is NullIndex
            // We check all blocks; in practice this is called rarely (only during Grow)
            for (int i = 1; i < _blockCount; i++)
            {
                if (_blocks[i].Size > 0 && _blocks[i].NextPhysical == NullIndex)
                {
                    return i;
                }
            }

            return NullIndex;
        }

        /// <summary>Allocates a new block header index, reusing recycled slots when available.</summary>
        private int AllocBlockHeader()
        {
            if (_recycledCount > 0)
            {
                _recycledCount--;
                return _recycledIndices[_recycledCount];
            }

            if (_blockCount >= _blocks.Length)
            {
                Array.Resize(ref _blocks, _blocks.Length * 2);
            }

            int idx = _blockCount;
            _blockCount++;
            return idx;
        }

        /// <summary>Returns a block header index to the recycled pool for future reuse.</summary>
        private void RecycleBlockHeader(int index)
        {
            // Note: offset mapping is removed by the caller (MergeBlockWithNext)
            // Clear the header to prevent stale data
            _blocks[index] = default;

            if (_recycledCount >= _recycledIndices.Length)
            {
                Array.Resize(ref _recycledIndices, _recycledIndices.Length * 2);
            }

            _recycledIndices[_recycledCount] = index;
            _recycledCount++;
        }

        /// <summary>Block header node used for both the physical block chain and segregated free-lists.</summary>
        private struct BlockHeader
        {
            /// <summary>Starting element offset of this block in the allocator's address space.</summary>
            public int Offset;

            /// <summary>Number of elements in this block.</summary>
            public int Size;

            /// <summary>Whether this block is free (available for allocation).</summary>
            public bool IsFree;

            /// <summary>Index of the previous block in physical (address-order) chain. -1 if first.</summary>
            public int PrevPhysical;

            /// <summary>Index of the next block in physical (address-order) chain. -1 if last.</summary>
            public int NextPhysical;

            /// <summary>Next block in the segregated free-list for this size class. -1 if tail.</summary>
            public int NextFree;

            /// <summary>Previous block in the segregated free-list for this size class. -1 if head.</summary>
            public int PrevFree;
        }
    }
}
