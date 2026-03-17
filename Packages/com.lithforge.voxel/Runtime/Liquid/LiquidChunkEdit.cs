using System.Runtime.InteropServices;
using Lithforge.Voxel.Block;
using Unity.Mathematics;

namespace Lithforge.Voxel.Liquid
{
    /// <summary>
    /// A single voxel change produced by <see cref="LiquidSimJob"/>.
    /// The job cannot call ChunkManager directly (Burst + thread safety),
    /// so it outputs these structs. <see cref="LiquidScheduler"/> applies them
    /// on the main thread after job completion.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct LiquidChunkEdit
    {
        /// <summary>
        /// Flat voxel index within the target chunk (y*1024 + z*32 + x).
        /// </summary>
        public int FlatIndex;

        /// <summary>
        /// Chunk offset from the job's own chunk coordinate.
        /// (0,0,0) = same chunk. (+1,0,0) = +X neighbor, etc.
        /// </summary>
        public int3 ChunkOffset;

        /// <summary>New liquid cell byte to write into LiquidData.</summary>
        public byte NewLiquidCell;

        /// <summary>New StateId to write via ChunkManager.SetBlock.</summary>
        public StateId NewStateId;
    }
}
