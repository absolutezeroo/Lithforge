namespace Lithforge.Runtime.Rendering
{
    /// <summary>
    /// Tracks a chunk's allocation within a MegaMeshBuffer.
    /// Stores arrays of allocated page indices (not necessarily contiguous)
    /// plus actual written vertex/index counts.
    /// </summary>
    public struct MegaMeshSlot
    {
        public static readonly MegaMeshSlot Invalid = new MegaMeshSlot
        {
            VertexPages = null,
            IndexPages = null,
            VertexCount = 0,
            IndexCount = 0,
        };

        /// <summary>
        /// Allocated vertex page indices within the mega-mesh buffer.
        /// Pages may be non-contiguous. Local vertex N maps to
        /// VertexPages[N / pageSize] * pageSize + (N % pageSize).
        /// </summary>
        public int[] VertexPages;

        /// <summary>
        /// Allocated index page indices within the mega-mesh buffer.
        /// Pages may be non-contiguous.
        /// </summary>
        public int[] IndexPages;

        /// <summary>
        /// Actual number of vertices written.
        /// </summary>
        public int VertexCount;

        /// <summary>
        /// Actual number of indices written.
        /// </summary>
        public int IndexCount;

        public bool IsValid
        {
            get { return VertexPages != null; }
        }
    }
}
