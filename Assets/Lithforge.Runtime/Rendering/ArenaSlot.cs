namespace Lithforge.Runtime.Rendering
{
    /// <summary>
    ///     Vertex/index region owned by one chunk in a single BufferArena.
    ///     Vertex and index offsets are independent (allocated from separate TLSF instances).
    ///     Owner: BufferArena (stored in its _slots dictionary).
    /// </summary>
    internal struct ArenaSlot
    {
        /// <summary>Starting vertex element index allocated from the vertex TLSF allocator.</summary>
        public int VertexOffset;

        /// <summary>Number of active vertices written to the CPU mirror (may be less than the TLSF block size).</summary>
        public int VertexCount;

        /// <summary>Starting index element index allocated from the index TLSF allocator.</summary>
        public int IndexOffset;

        /// <summary>Number of active indices written to the CPU mirror.</summary>
        public int IndexCount;
    }
}
