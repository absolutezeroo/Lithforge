using System.Collections.Generic;

using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;

using Unity.Collections;
using Unity.Mathematics;

namespace Lithforge.WorldGen.Decoration
{
    /// <summary>
    /// Thread-safe store for decoration blocks that overflow into ungenerated neighbor chunks.
    /// Blocks are queued by the source chunk's decoration pass and consumed when the target chunk generates.
    /// </summary>
    public sealed class PendingDecorationStore
    {
        /// <summary>Synchronization lock for concurrent Add/TryConsume access.</summary>
        private readonly object _lock = new();

        /// <summary>Pending blocks keyed by target chunk coordinate.</summary>
        private readonly Dictionary<int3, List<PendingBlock>> _pending = new();

        /// <summary>Queues a pending block for the given target chunk coordinate.</summary>
        public void Add(int3 chunkCoord, PendingBlock block)
        {
            lock (_lock)
            {
                if (!_pending.TryGetValue(chunkCoord, out List<PendingBlock> list))
                {
                    list = new List<PendingBlock>();
                    _pending[chunkCoord] = list;
                }

                list.Add(block);
            }
        }

        /// <summary>Atomically removes and returns all pending blocks for the given chunk, if any exist.</summary>
        public bool TryConsume(int3 chunkCoord, out List<PendingBlock> blocks)
        {
            lock (_lock)
            {
                if (_pending.TryGetValue(chunkCoord, out blocks))
                {
                    _pending.Remove(chunkCoord);

                    return true;
                }

                blocks = null;

                return false;
            }
        }

        /// <summary>Consumes and writes all pending blocks into the chunk data, replacing only air voxels.</summary>
        public void ApplyPending(int3 chunkCoord, NativeArray<StateId> chunkData)
        {
            if (TryConsume(chunkCoord, out List<PendingBlock> blocks))
            {
                for (int i = 0; i < blocks.Count; i++)
                {
                    PendingBlock pending = blocks[i];
                    int3 pos = pending.LocalPosition;

                    if (pos.x is >= 0 and < ChunkConstants.Size &&
                        pos.y is >= 0 and < ChunkConstants.Size &&
                        pos.z is >= 0 and < ChunkConstants.Size)
                    {
                        int index = ChunkData.GetIndex(pos.x, pos.y, pos.z);
                        StateId current = chunkData[index];

                        if (current.Value == 0)
                        {
                            chunkData[index] = pending.State;
                        }
                    }
                }
            }
        }
    }
}
