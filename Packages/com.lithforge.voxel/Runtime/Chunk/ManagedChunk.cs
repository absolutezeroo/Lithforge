using Lithforge.Voxel.Block;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lithforge.Voxel.Chunk
{
    public sealed class ManagedChunk
    {
        public int3 Coord { get; }
        public ChunkState State { get; set; }
        public NativeArray<StateId> Data { get; set; }
        public NativeArray<byte> LightData { get; set; }
        public JobHandle ActiveJobHandle { get; set; }
        public bool NeedsRemesh { get; set; }

        public ManagedChunk(int3 coord, NativeArray<StateId> data)
        {
            Coord = coord;
            State = ChunkState.Unloaded;
            Data = data;
        }
    }
}
