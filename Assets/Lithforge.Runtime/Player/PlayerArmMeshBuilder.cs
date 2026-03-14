using Unity.Mathematics;
using UnityEngine;

namespace Lithforge.Runtime.Player
{
    /// <summary>
    /// Generates static vertex and index arrays for the first-person arm mesh.
    /// Each arm is a box (6 faces, 24 vertices, 36 indices). Both arms have
    /// a base layer and an overlay layer (sleeve), for a total of 4 boxes.
    /// Vertices are positioned relative to the part pivot so the shader can
    /// apply per-part rotation matrices.
    /// </summary>
    public static class PlayerArmMeshBuilder
    {
        /// <summary>Overlay inflation in model units (pixels). +0.25 per side for arms.</summary>
        private const float ArmOverlayInflation = 0.25f;

        /// <summary>Number of vertices per box (6 faces x 4 verts).</summary>
        private const int VertsPerBox = 24;

        /// <summary>Number of indices per box (6 faces x 2 triangles x 3 indices).</summary>
        private const int IndicesPerBox = 36;

        /// <summary>
        /// Total vertex count: 4 boxes (right base, left base, right overlay, left overlay).
        /// </summary>
        public const int TotalVertexCount = VertsPerBox * 4;

        /// <summary>
        /// Total index count: 4 boxes.
        /// </summary>
        public const int TotalIndexCount = IndicesPerBox * 4;

        /// <summary>
        /// Index count for the base layer draw call (right arm base + left arm base).
        /// </summary>
        public const int BaseLayerIndexCount = IndicesPerBox * 2;

        /// <summary>
        /// Index count for the overlay layer draw call (right arm overlay + left arm overlay).
        /// </summary>
        public const int OverlayLayerIndexCount = IndicesPerBox * 2;

        /// <summary>
        /// Builds all arm vertices and indices.
        /// Vertex layout: [rightBase(24), leftBase(24), rightOverlay(24), leftOverlay(24)]
        /// Index layout:  [rightBase(36), leftBase(36), rightOverlay(36), leftOverlay(36)]
        /// </summary>
        public static void Build(bool isSlim, out PlayerArmVertex[] vertices, out int[] indices)
        {
            vertices = new PlayerArmVertex[TotalVertexCount];
            indices = new int[TotalIndexCount];

            int armWidth = isSlim ? 3 : 4;

            // Right arm dimensions and positioning
            float3 rightMin = isSlim ? new float3(-7.5f, 12f, -2f) : new float3(-8f, 12f, -2f);
            float3 rightPivot = isSlim ? new float3(-5f, 21.5f, 0f) : new float3(-5f, 22f, 0f);
            float3 rightSize = new float3(armWidth, 12f, 4f);

            // Left arm dimensions and positioning
            float3 leftMin = isSlim ? new float3(4.5f, 12f, -2f) : new float3(4f, 12f, -2f);
            float3 leftPivot = isSlim ? new float3(5f, 21.5f, 0f) : new float3(5f, 22f, 0f);
            float3 leftSize = new float3(armWidth, 12f, 4f);

            // UV part definitions
            SkinPartDefinition rightBase = isSlim ? SkinUVMapper.RightArmBase3 : SkinUVMapper.RightArmBase4;
            SkinPartDefinition rightOverlay = isSlim ? SkinUVMapper.RightArmOverlay3 : SkinUVMapper.RightArmOverlay4;
            SkinPartDefinition leftBase = isSlim ? SkinUVMapper.LeftArmBase3 : SkinUVMapper.LeftArmBase4;
            SkinPartDefinition leftOverlay = isSlim ? SkinUVMapper.LeftArmOverlay3 : SkinUVMapper.LeftArmOverlay4;

            int vertOffset = 0;
            int idxOffset = 0;

            // Box 0: Right arm base (partID=2)
            BuildBox(vertices, indices, ref vertOffset, ref idxOffset,
                rightMin, rightSize, rightPivot, 0f, 2, 0, rightBase);

            // Box 1: Left arm base (partID=3)
            BuildBox(vertices, indices, ref vertOffset, ref idxOffset,
                leftMin, leftSize, leftPivot, 0f, 3, 0, leftBase);

            // Box 2: Right arm overlay (partID=2, inflated, overlay flag)
            BuildBox(vertices, indices, ref vertOffset, ref idxOffset,
                rightMin, rightSize, rightPivot, ArmOverlayInflation, 2, 1, rightOverlay);

            // Box 3: Left arm overlay (partID=3, inflated, overlay flag)
            BuildBox(vertices, indices, ref vertOffset, ref idxOffset,
                leftMin, leftSize, leftPivot, ArmOverlayInflation, 3, 1, leftOverlay);
        }

        /// <summary>
        /// Builds a single box (24 vertices, 36 indices) and writes into the arrays
        /// at the current offsets.
        /// </summary>
        private static void BuildBox(
            PlayerArmVertex[] vertices, int[] indices,
            ref int vertOffset, ref int idxOffset,
            float3 min, float3 size, float3 pivot,
            float inflation, uint partID, uint overlayFlag,
            SkinPartDefinition uvPart)
        {
            // Inflate the box for overlay layers
            float3 inflatedMin = min - new float3(inflation);
            float3 inflatedSize = size + new float3(inflation * 2f);
            float3 inflatedMax = inflatedMin + inflatedSize;

            // Pivot-relative corners, converted from model units (pixels) to block units (÷16)
            float3 lo = (inflatedMin - pivot) / 16f;
            float3 hi = (inflatedMax - pivot) / 16f;

            int baseVert = vertOffset;

            // Generate 6 faces
            for (int face = 0; face < 6; face++)
            {
                SkinFaceDirection dir = (SkinFaceDirection)face;
                Vector4 uv = SkinUVMapper.GetFaceUV(uvPart, dir);
                float uMin = uv.x;
                float vMin = uv.y;
                float uMax = uv.z;
                float vMax = uv.w;

                float3 normal;
                float3 v0;
                float3 v1;
                float3 v2;
                float3 v3;

                // Vertex winding: v0-v1-v2, v0-v2-v3 (CCW when viewed from outside)
                switch (dir)
                {
                    case SkinFaceDirection.Top: // +Y
                        normal = new float3(0, 1, 0);
                        v0 = new float3(lo.x, hi.y, lo.z);
                        v1 = new float3(lo.x, hi.y, hi.z);
                        v2 = new float3(hi.x, hi.y, hi.z);
                        v3 = new float3(hi.x, hi.y, lo.z);
                        break;
                    case SkinFaceDirection.Bottom: // -Y
                        normal = new float3(0, -1, 0);
                        v0 = new float3(lo.x, lo.y, hi.z);
                        v1 = new float3(lo.x, lo.y, lo.z);
                        v2 = new float3(hi.x, lo.y, lo.z);
                        v3 = new float3(hi.x, lo.y, hi.z);
                        break;
                    case SkinFaceDirection.Right: // -X
                        normal = new float3(-1, 0, 0);
                        v0 = new float3(lo.x, hi.y, hi.z);
                        v1 = new float3(lo.x, hi.y, lo.z);
                        v2 = new float3(lo.x, lo.y, lo.z);
                        v3 = new float3(lo.x, lo.y, hi.z);
                        break;
                    case SkinFaceDirection.Front: // +Z
                        normal = new float3(0, 0, 1);
                        v0 = new float3(hi.x, hi.y, hi.z);
                        v1 = new float3(lo.x, hi.y, hi.z);
                        v2 = new float3(lo.x, lo.y, hi.z);
                        v3 = new float3(hi.x, lo.y, hi.z);
                        break;
                    case SkinFaceDirection.Left: // +X
                        normal = new float3(1, 0, 0);
                        v0 = new float3(hi.x, hi.y, lo.z);
                        v1 = new float3(hi.x, hi.y, hi.z);
                        v2 = new float3(hi.x, lo.y, hi.z);
                        v3 = new float3(hi.x, lo.y, lo.z);
                        break;
                    case SkinFaceDirection.Back: // -Z
                        normal = new float3(0, 0, -1);
                        v0 = new float3(lo.x, hi.y, lo.z);
                        v1 = new float3(hi.x, hi.y, lo.z);
                        v2 = new float3(hi.x, lo.y, lo.z);
                        v3 = new float3(lo.x, lo.y, lo.z);
                        break;
                    default:
                        normal = float3.zero;
                        v0 = v1 = v2 = v3 = float3.zero;
                        break;
                }

                // Write 4 vertices
                vertices[vertOffset + 0] = new PlayerArmVertex
                {
                    Position = v0, Normal = normal,
                    UV = new float2(uMin, vMin),
                    PartID = partID, Flags = overlayFlag,
                };
                vertices[vertOffset + 1] = new PlayerArmVertex
                {
                    Position = v1, Normal = normal,
                    UV = new float2(uMax, vMin),
                    PartID = partID, Flags = overlayFlag,
                };
                vertices[vertOffset + 2] = new PlayerArmVertex
                {
                    Position = v2, Normal = normal,
                    UV = new float2(uMax, vMax),
                    PartID = partID, Flags = overlayFlag,
                };
                vertices[vertOffset + 3] = new PlayerArmVertex
                {
                    Position = v3, Normal = normal,
                    UV = new float2(uMin, vMax),
                    PartID = partID, Flags = overlayFlag,
                };

                // Write 6 indices (two triangles: 0-1-2, 0-2-3)
                indices[idxOffset + 0] = baseVert + face * 4 + 0;
                indices[idxOffset + 1] = baseVert + face * 4 + 1;
                indices[idxOffset + 2] = baseVert + face * 4 + 2;
                indices[idxOffset + 3] = baseVert + face * 4 + 0;
                indices[idxOffset + 4] = baseVert + face * 4 + 2;
                indices[idxOffset + 5] = baseVert + face * 4 + 3;

                vertOffset += 4;
                idxOffset += 6;
            }
        }
    }
}
