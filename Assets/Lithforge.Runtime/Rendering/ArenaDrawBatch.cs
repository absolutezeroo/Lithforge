using UnityEngine;

namespace Lithforge.Runtime.Rendering
{
    /// <summary>
    ///     Per-arena draw info for one RenderPrimitivesIndexedIndirect call.
    ///     Returned by BufferArenaPool.GetDrawBatch for the multi-arena draw loop.
    /// </summary>
    public readonly struct ArenaDrawBatch
    {
        /// <summary>The per-chunk indirect args buffer for this arena.</summary>
        public readonly GraphicsBuffer PerChunkArgsBuffer;

        /// <summary>The vertex data buffer for this arena.</summary>
        public readonly GraphicsBuffer VertexBuffer;

        /// <summary>The index buffer for this arena.</summary>
        public readonly GraphicsBuffer IndexBuffer;

        /// <summary>Number of active draw commands (the commandCount for RenderPrimitivesIndexedIndirect).</summary>
        public readonly int CommandCount;

        /// <summary>Whether this arena has any geometry to draw.</summary>
        public readonly bool HasGeometry;

        /// <summary>Creates a new ArenaDrawBatch with the specified GPU resources and command count.</summary>
        public ArenaDrawBatch(
            GraphicsBuffer perChunkArgsBuffer,
            GraphicsBuffer vertexBuffer,
            GraphicsBuffer indexBuffer,
            int commandCount,
            bool hasGeometry)
        {
            PerChunkArgsBuffer = perChunkArgsBuffer;
            VertexBuffer = vertexBuffer;
            IndexBuffer = indexBuffer;
            CommandCount = commandCount;
            HasGeometry = hasGeometry;
        }
    }
}
