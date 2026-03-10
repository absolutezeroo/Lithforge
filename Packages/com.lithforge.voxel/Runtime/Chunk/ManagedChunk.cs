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

        /// <summary>
        /// Current LOD level for this chunk. 0 = full detail, 1-3 = downsampled.
        /// </summary>
        public int LODLevel { get; set; }

        /// <summary>
        /// The LOD level that is currently rendered. -1 means no LOD mesh uploaded yet.
        /// Used to detect LOD transitions.
        /// </summary>
        public int RenderedLODLevel { get; set; }

        public ManagedChunk(int3 coord, NativeArray<StateId> data)
        {
            Coord = coord;
            State = ChunkState.Unloaded;
            Data = data;
            LODLevel = 0;
            RenderedLODLevel = -1;
        }
    }
}
