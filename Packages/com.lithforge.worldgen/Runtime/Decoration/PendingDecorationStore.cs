using System.Collections.Generic;
using Lithforge.Voxel.Block;
using Unity.Mathematics;

namespace Lithforge.WorldGen.Decoration
{
    public sealed class PendingDecorationStore
    {
        private readonly Dictionary<int3, List<PendingBlock>> _pending = new();

        private readonly object _lock = new();

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

        public void ApplyPending(int3 chunkCoord, Unity.Collections.NativeArray<StateId> chunkData)
        {
            if (TryConsume(chunkCoord, out List<PendingBlock> blocks))
            {
                for (int i = 0; i < blocks.Count; i++)
                {
                    PendingBlock pending = blocks[i];
                    int3 pos = pending.LocalPosition;

                    if (pos.x >= 0 && pos.x < Lithforge.Voxel.Chunk.ChunkConstants.Size &&
                        pos.y >= 0 && pos.y < Lithforge.Voxel.Chunk.ChunkConstants.Size &&
                        pos.z >= 0 && pos.z < Lithforge.Voxel.Chunk.ChunkConstants.Size)
                    {
                        int index = Lithforge.Voxel.Chunk.ChunkData.GetIndex(pos.x, pos.y, pos.z);
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
