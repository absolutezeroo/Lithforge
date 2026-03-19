using Lithforge.Voxel.Block;

namespace Lithforge.Voxel.Chunk
{
    /// <summary>
    /// A block edit that was deferred because the chunk was in the Meshing state.
    /// Applied after the in-flight mesh job completes (in MeshScheduler.PollCompleted).
    /// </summary>
    public struct DeferredEdit
    {
        /// <summary>Flat voxel index within the chunk (0 to Volume-1).</summary>
        public int FlatIndex;

        /// <summary>Block state before the edit, used for undo or light removal seeding.</summary>
        public StateId OldState;

        /// <summary>Block state to write when the deferred edit is applied.</summary>
        public StateId NewState;
    }
}
