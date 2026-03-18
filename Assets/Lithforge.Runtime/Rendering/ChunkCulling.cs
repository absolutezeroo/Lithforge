using Unity.Mathematics;
using UnityEngine;

namespace Lithforge.Runtime.Rendering
{
    /// <summary>
    /// Manages camera frustum planes and performs per-chunk AABB visibility tests.
    /// </summary>
    public sealed class ChunkCulling
    {
        private readonly Plane[] _frustumPlanes = new Plane[6];
        private bool _frustumValid;

        /// <summary>
        /// Recalculates frustum planes from the given camera.
        /// Call once per frame before any IsInFrustum queries.
        /// </summary>
        public void UpdateFrustum(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            GeometryUtility.CalculateFrustumPlanes(camera, _frustumPlanes);
            _frustumValid = true;
        }

        /// <summary>
        /// Returns true if the chunk AABB intersects the current frustum.
        /// Returns true if no camera was available when frustum was last updated.
        /// </summary>
        public bool IsInFrustum(int3 chunkCoord)
        {
            if (!_frustumValid)
            {
                return true;
            }

            float3 min = new(
                chunkCoord.x * Lithforge.Voxel.Chunk.ChunkConstants.Size,
                chunkCoord.y * Lithforge.Voxel.Chunk.ChunkConstants.Size,
                chunkCoord.z * Lithforge.Voxel.Chunk.ChunkConstants.Size);
            float3 max = min + new float3(
                Lithforge.Voxel.Chunk.ChunkConstants.Size,
                Lithforge.Voxel.Chunk.ChunkConstants.Size,
                Lithforge.Voxel.Chunk.ChunkConstants.Size);

            Bounds bounds = new();
            bounds.SetMinMax(
                new Vector3(min.x, min.y, min.z),
                new Vector3(max.x, max.y, max.z));

            return GeometryUtility.TestPlanesAABB(_frustumPlanes, bounds);
        }
    }
}
