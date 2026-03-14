using System.Collections.Generic;
using Lithforge.Core.Data;
using Lithforge.Voxel.Block;
using Unity.Mathematics;

namespace Lithforge.Runtime.Player
{
    /// <summary>
    /// Builds vertex and index data for held items in first-person view.
    /// Block items are rendered as a 6-face cube with atlas textures.
    /// Flat items are rendered as a 2-face quad with the item sprite.
    /// Display transforms are read from BlockModel assets via ItemDisplayTransformLookup.
    /// </summary>
    public static class HeldItemMeshBuilder
    {
        /// <summary>
        /// Right hand attachment point in model space (classic arm).
        /// Matches the spec: [-6, 15, 1] scaled to block units.
        /// </summary>
        private static readonly float3 s_rightHandLocator = new float3(-6f, 15f, 1f) / 16f;

        /// <summary>
        /// Builds a 6-face cube for a held block item.
        /// The displayMatrix comes from the item's BlockModel parent chain
        /// (resolved via ItemDisplayTransformLookup during the content pipeline).
        /// </summary>
        public static void BuildBlockItem(
            StateRegistry stateRegistry,
            ResourceId blockId,
            float4x4 displayMatrix,
            out HeldItemVertex[] vertices,
            out int[] indices)
        {
            // Find the StateRegistryEntry matching this block ResourceId
            StateRegistryEntry entry = FindEntryByBlockId(stateRegistry.Entries, blockId);

            if (entry == null)
            {
                vertices = new HeldItemVertex[0];
                indices = new int[0];
                return;
            }

            BlockStateCompact compact = stateRegistry.GetState(new StateId(entry.BaseStateId));

            // 6 faces * 4 verts = 24 verts, 6 faces * 6 indices = 36 indices
            vertices = new HeldItemVertex[24];
            indices = new int[36];

            float3 handOffset = s_rightHandLocator;

            float4x4 displayMat = displayMatrix;
            float3x3 displayRot = new float3x3(displayMat.c0.xyz, displayMat.c1.xyz, displayMat.c2.xyz);

            // Unit cube: 1x1x1 centered at origin (Minecraft blocks are 16x16x16 = 1 block)
            float h = 0.5f;

            int vertIdx = 0;
            int idxIdx = 0;

            // Face order: +X, -X, +Y, -Y, +Z, -Z
            ushort[] texIndices = new ushort[6];
            texIndices[0] = compact.TexEast;
            texIndices[1] = compact.TexWest;
            texIndices[2] = compact.TexUp;
            texIndices[3] = compact.TexDown;
            texIndices[4] = compact.TexSouth;
            texIndices[5] = compact.TexNorth;

            float3[] normals = new float3[6];
            normals[0] = new float3(1, 0, 0);
            normals[1] = new float3(-1, 0, 0);
            normals[2] = new float3(0, 1, 0);
            normals[3] = new float3(0, -1, 0);
            normals[4] = new float3(0, 0, 1);
            normals[5] = new float3(0, 0, -1);

            // Face vertex positions (unit cube, before display transform)
            float3[][] faceVerts = new float3[6][];

            // +X face
            faceVerts[0] = new float3[]
            {
                new float3(h, h, -h),
                new float3(h, h, h),
                new float3(h, -h, h),
                new float3(h, -h, -h),
            };
            // -X face
            faceVerts[1] = new float3[]
            {
                new float3(-h, h, h),
                new float3(-h, h, -h),
                new float3(-h, -h, -h),
                new float3(-h, -h, h),
            };
            // +Y face
            faceVerts[2] = new float3[]
            {
                new float3(-h, h, -h),
                new float3(-h, h, h),
                new float3(h, h, h),
                new float3(h, h, -h),
            };
            // -Y face
            faceVerts[3] = new float3[]
            {
                new float3(-h, -h, h),
                new float3(-h, -h, -h),
                new float3(h, -h, -h),
                new float3(h, -h, h),
            };
            // +Z face
            faceVerts[4] = new float3[]
            {
                new float3(h, h, h),
                new float3(-h, h, h),
                new float3(-h, -h, h),
                new float3(h, -h, h),
            };
            // -Z face
            faceVerts[5] = new float3[]
            {
                new float3(-h, h, -h),
                new float3(h, h, -h),
                new float3(h, -h, -h),
                new float3(-h, -h, -h),
            };

            for (int face = 0; face < 6; face++)
            {
                float3 rotN = math.normalize(math.mul(displayRot, normals[face]));

                for (int v = 0; v < 4; v++)
                {
                    float3 pos = math.mul(displayMat, new float4(faceVerts[face][v], 1f)).xyz;
                    pos += handOffset;

                    vertices[vertIdx + v] = new HeldItemVertex
                    {
                        Position = pos,
                        Normal = rotN,
                        UV = GetBlockFaceUV(v),
                        TexIndex = texIndices[face],
                        Padding = 0,
                    };
                }

                int baseV = vertIdx;
                indices[idxIdx + 0] = baseV + 0;
                indices[idxIdx + 1] = baseV + 1;
                indices[idxIdx + 2] = baseV + 2;
                indices[idxIdx + 3] = baseV + 0;
                indices[idxIdx + 4] = baseV + 2;
                indices[idxIdx + 5] = baseV + 3;

                vertIdx += 4;
                idxIdx += 6;
            }
        }

        /// <summary>
        /// Builds a 2-face quad for a flat held item (tools, sticks, etc.).
        /// The quad faces forward from the hand with both sides visible.
        /// The displayMatrix comes from the item's BlockModel parent chain
        /// (resolved via ItemDisplayTransformLookup during the content pipeline).
        /// </summary>
        public static void BuildFlatItem(
            float4x4 displayMatrix,
            out HeldItemVertex[] vertices,
            out int[] indices)
        {
            // 2 faces * 4 verts = 8 verts, 2 faces * 6 indices = 12 indices
            vertices = new HeldItemVertex[8];
            indices = new int[12];

            float3 handOffset = s_rightHandLocator;

            float4x4 displayMat = displayMatrix;

            // Unit quad: 1x1 block centered at origin, bottom at y=0
            // (Minecraft item sprites are 16x16 pixels = 1x1 block)
            float3[] quadVerts = new float3[]
            {
                new float3(-0.5f, 1f, 0f),
                new float3(0.5f, 1f, 0f),
                new float3(0.5f, 0f, 0f),
                new float3(-0.5f, 0f, 0f),
            };

            float3 frontNormal = new float3(0, 0, 1);
            float3 rotFrontNormal = math.normalize(
                math.mul((float3x3)displayMat, frontNormal));
            float3 rotBackNormal = -rotFrontNormal;

            for (int v = 0; v < 4; v++)
            {
                // Apply display transform then hand locator offset
                float3 pos = math.mul(displayMat, new float4(quadVerts[v], 1f)).xyz;
                pos += handOffset;

                vertices[v] = new HeldItemVertex
                {
                    Position = pos,
                    Normal = rotFrontNormal,
                    UV = GetItemQuadUV(v),
                    TexIndex = 0,
                    Padding = 0,
                };
            }

            // Back face (same positions, reversed winding, flipped normal)
            for (int v = 0; v < 4; v++)
            {
                vertices[4 + v] = vertices[v];
                vertices[4 + v].Normal = rotBackNormal;
            }

            // Front face indices
            indices[0] = 0; indices[1] = 1; indices[2] = 2;
            indices[3] = 0; indices[4] = 2; indices[5] = 3;

            // Back face indices (reversed winding)
            indices[6] = 4; indices[7] = 6; indices[8] = 5;
            indices[9] = 4; indices[10] = 7; indices[11] = 6;
        }

        private static float2 GetBlockFaceUV(int vertexIndex)
        {
            switch (vertexIndex)
            {
                case 0: return new float2(0f, 0f);
                case 1: return new float2(1f, 0f);
                case 2: return new float2(1f, 1f);
                case 3: return new float2(0f, 1f);
                default: return float2.zero;
            }
        }

        private static float2 GetItemQuadUV(int vertexIndex)
        {
            switch (vertexIndex)
            {
                case 0: return new float2(0f, 1f);
                case 1: return new float2(1f, 1f);
                case 2: return new float2(1f, 0f);
                case 3: return new float2(0f, 0f);
                default: return float2.zero;
            }
        }

        /// <summary>
        /// Finds a StateRegistryEntry by block ResourceId. O(n) scan, called infrequently.
        /// </summary>
        private static StateRegistryEntry FindEntryByBlockId(
            IReadOnlyList<StateRegistryEntry> entries, ResourceId blockId)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Id.Equals(blockId))
                {
                    return entries[i];
                }
            }

            return null;
        }
    }
}
