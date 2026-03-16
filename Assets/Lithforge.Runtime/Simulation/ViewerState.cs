using Unity.Mathematics;

namespace Lithforge.Runtime.Simulation
{
    /// <summary>
    /// Camera-agnostic snapshot of the viewer's position and look direction.
    /// Used by schedulers and chunk loading to determine what to prioritize,
    /// without coupling to UnityEngine.Camera or Transform.
    /// </summary>
    public struct ViewerState
    {
        /// <summary>World-space position of the viewer (camera).</summary>
        public float3 Position;

        /// <summary>
        /// Normalized forward direction (horizontal only, Y=0) for priority ordering.
        /// </summary>
        public float3 ForwardXZ;

        /// <summary>
        /// Chunk coordinate of the viewer (position divided by chunk size, floored).
        /// </summary>
        public int3 ChunkCoord;
    }
}
