using Lithforge.Voxel.Block;

namespace Lithforge.Voxel.Chunk
{
    /// <summary>
    /// A block edit that was deferred because the chunk was in the Meshing state.
    /// Applied after the in-flight mesh job completes (in MeshScheduler.PollCompleted).
    /// </summary>
    public struct DeferredEdit
    {
        public int FlatIndex;
        public StateId OldState;
        public StateId NewState;
    }
}
