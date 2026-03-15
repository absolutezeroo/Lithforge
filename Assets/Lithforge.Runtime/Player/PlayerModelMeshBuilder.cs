using Unity.Mathematics;
using UnityEngine;

namespace Lithforge.Runtime.Player
{
    /// <summary>
    /// Generates static vertex and index arrays for the full player model mesh.
    /// Builds 12 boxes: 6 base parts + 6 overlay parts (hat, jacket, sleeves, pants).
    /// Head indices are placed at the END of each layer so first-person mode can
    /// simply reduce indexCount to exclude them.
    ///
    /// Index layout (36 indices per box):
    /// Base:    [body(0-35), rArm(36-71), lArm(72-107), rLeg(108-143), lLeg(144-179), head(180-215)]
    /// Overlay: [jacket(216-251), rSleeve(252-287), lSleeve(288-323), rPants(324-359), lPants(360-395), hat(396-431)]
    ///
    /// First-person draws 180 indices per layer (head/hat excluded).
    /// Third-person draws 216 indices per layer (all parts).
    /// </summary>
    public static class PlayerModelMeshBuilder
    {
        private const float HeadOverlayInflation = 0.5f;
        private const float BodyOverlayInflation = 0.25f;

        private const int VertsPerBox = 24;
        private const int IndicesPerBox = 36;

        /// <summary>Number of body parts (head, body, rightArm, leftArm, rightLeg, leftLeg).</summary>
        private const int PartCount = 6;

        /// <summary>Total vertex count: 12 boxes (6 base + 6 overlay).</summary>
        public const int TotalVertexCount = VertsPerBox * PartCount * 2;

        /// <summary>Total index count: 12 boxes.</summary>
        public const int TotalIndexCount = IndicesPerBox * PartCount * 2;

        /// <summary>Index count per layer excluding head (5 body parts).</summary>
        public const int FirstPersonLayerIndexCount = IndicesPerBox * 5;

        /// <summary>Index count per layer including head (6 body parts).</summary>
        public const int ThirdPersonLayerIndexCount = IndicesPerBox * 6;

        /// <summary>
        /// Builds all player model vertices and indices.
        /// Parts are ordered body-first with head last in each layer for easy FP exclusion.
        /// </summary>
        public static void Build(bool isSlim, out PlayerModelVertex[] vertices, out int[] indices)
        {
            vertices = new PlayerModelVertex[TotalVertexCount];
            indices = new int[TotalIndexCount];

            int armWidth = isSlim ? 3 : 4;

            // Part dimensions in model units (pixels)
            // Pivots are the rotation center for each part

            // Body: 8w x 12h x 4d, pivot at (0, 24, 0) — top of body
            float3 bodyMin = new float3(-4f, 12f, -2f);
            float3 bodySize = new float3(8f, 12f, 4f);
            float3 bodyPivot = new float3(0f, 24f, 0f);

            // Right Arm: (armWidth)w x 12h x 4d
            float3 rightArmMin = isSlim
                ? new float3(-4f - 3f, 12f, -2f)
                : new float3(-4f - 4f, 12f, -2f);
            float3 rightArmSize = new float3(armWidth, 12f, 4f);
            float3 rightArmPivot = isSlim
                ? new float3(-5.5f, 22f, 0f)
                : new float3(-6f, 22f, 0f);

            // Left Arm: (armWidth)w x 12h x 4d
            float3 leftArmMin = new float3(4f, 12f, -2f);
            float3 leftArmSize = new float3(armWidth, 12f, 4f);
            float3 leftArmPivot = isSlim
                ? new float3(5.5f, 22f, 0f)
                : new float3(6f, 22f, 0f);

            // Right Leg: 4w x 12h x 4d
            float3 rightLegMin = new float3(-4f, 0f, -2f);
            float3 rightLegSize = new float3(4f, 12f, 4f);
            float3 rightLegPivot = new float3(-2f, 12f, 0f);

            // Left Leg: 4w x 12h x 4d
            float3 leftLegMin = new float3(0f, 0f, -2f);
            float3 leftLegSize = new float3(4f, 12f, 4f);
            float3 leftLegPivot = new float3(2f, 12f, 0f);

            // Head: 8w x 8h x 8d
            float3 headMin = new float3(-4f, 24f, -4f);
            float3 headSize = new float3(8f, 8f, 8f);
            float3 headPivot = new float3(0f, 24f, 0f);

            // UV part definitions
            SkinPartDefinition rightArmBase = isSlim ? SkinUVMapper.RightArmBase3 : SkinUVMapper.RightArmBase4;
            SkinPartDefinition rightArmOverlay = isSlim ? SkinUVMapper.RightArmOverlay3 : SkinUVMapper.RightArmOverlay4;
            SkinPartDefinition leftArmBase = isSlim ? SkinUVMapper.LeftArmBase3 : SkinUVMapper.LeftArmBase4;
            SkinPartDefinition leftArmOverlay = isSlim ? SkinUVMapper.LeftArmOverlay3 : SkinUVMapper.LeftArmOverlay4;

            int vertOffset = 0;
            int idxOffset = 0;

            // === BASE LAYER (body-first, head last) ===

            // Part 1 (body, partID=1)
            BuildBox(vertices, indices, ref vertOffset, ref idxOffset,
                bodyMin, bodySize, bodyPivot, 0f, 1, 0, SkinUVMapper.BodyBase);

            // Part 2 (right arm, partID=2)
            BuildBox(vertices, indices, ref vertOffset, ref idxOffset,
                rightArmMin, rightArmSize, rightArmPivot, 0f, 2, 0, rightArmBase);

            // Part 3 (left arm, partID=3)
            BuildBox(vertices, indices, ref vertOffset, ref idxOffset,
                leftArmMin, leftArmSize, leftArmPivot, 0f, 3, 0, leftArmBase);

            // Part 4 (right leg, partID=4)
            BuildBox(vertices, indices, ref vertOffset, ref idxOffset,
                rightLegMin, rightLegSize, rightLegPivot, 0f, 4, 0, SkinUVMapper.RightLegBase);

            // Part 5 (left leg, partID=5)
            BuildBox(vertices, indices, ref vertOffset, ref idxOffset,
                leftLegMin, leftLegSize, leftLegPivot, 0f, 5, 0, SkinUVMapper.LeftLegBase);

            // Part 6 (head, partID=0) — LAST in base layer
            BuildBox(vertices, indices, ref vertOffset, ref idxOffset,
                headMin, headSize, headPivot, 0f, 0, 0, SkinUVMapper.HeadBase);

            // === OVERLAY LAYER (body-first, hat last) ===

            // Jacket (body overlay, partID=1)
            BuildBox(vertices, indices, ref vertOffset, ref idxOffset,
                bodyMin, bodySize, bodyPivot, BodyOverlayInflation, 1, 1, SkinUVMapper.BodyOverlay);

            // Right sleeve (partID=2)
            BuildBox(vertices, indices, ref vertOffset, ref idxOffset,
                rightArmMin, rightArmSize, rightArmPivot, BodyOverlayInflation, 2, 1, rightArmOverlay);

            // Left sleeve (partID=3)
            BuildBox(vertices, indices, ref vertOffset, ref idxOffset,
                leftArmMin, leftArmSize, leftArmPivot, BodyOverlayInflation, 3, 1, leftArmOverlay);

            // Right pants (partID=4)
            BuildBox(vertices, indices, ref vertOffset, ref idxOffset,
                rightLegMin, rightLegSize, rightLegPivot, BodyOverlayInflation, 4, 1, SkinUVMapper.RightLegOverlay);

            // Left pants (partID=5)
            BuildBox(vertices, indices, ref vertOffset, ref idxOffset,
                leftLegMin, leftLegSize, leftLegPivot, BodyOverlayInflation, 5, 1, SkinUVMapper.LeftLegOverlay);

            // Hat (head overlay, partID=0) — LAST in overlay layer
            BuildBox(vertices, indices, ref vertOffset, ref idxOffset,
                headMin, headSize, headPivot, HeadOverlayInflation, 0, 1, SkinUVMapper.HeadOverlay);
        }

        /// <summary>
        /// Builds a single box (24 vertices, 36 indices) and writes into the arrays
        /// at the current offsets.
        /// </summary>
        private static void BuildBox(
            PlayerModelVertex[] vertices, int[] indices,
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
                vertices[vertOffset + 0] = new PlayerModelVertex
                {
                    Position = v0, Normal = normal,
                    UV = new float2(uMin, vMin),
                    PartID = partID, Flags = overlayFlag,
                };
                vertices[vertOffset + 1] = new PlayerModelVertex
                {
                    Position = v1, Normal = normal,
                    UV = new float2(uMax, vMin),
                    PartID = partID, Flags = overlayFlag,
                };
                vertices[vertOffset + 2] = new PlayerModelVertex
                {
                    Position = v2, Normal = normal,
                    UV = new float2(uMax, vMax),
                    PartID = partID, Flags = overlayFlag,
                };
                vertices[vertOffset + 3] = new PlayerModelVertex
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
