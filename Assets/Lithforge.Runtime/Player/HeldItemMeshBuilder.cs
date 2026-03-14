using System.Collections.Generic;
using System.Runtime.InteropServices;
using Lithforge.Core.Data;
using Lithforge.Voxel.Block;
using Unity.Mathematics;

namespace Lithforge.Runtime.Player
{
    /// <summary>
    /// Vertex format for held items rendered in first-person view.
    /// Uses the same StructuredBuffer approach as the arm mesh but with
    /// atlas texture coordinates instead of skin UVs.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct HeldItemVertex
    {
        /// <summary>Position in model space (relative to hand locator).</summary>
        public float3 Position;

        /// <summary>Face normal.</summary>
        public float3 Normal;

        /// <summary>UV coordinates for the texture atlas or item sprite.</summary>
        public float2 UV;

        /// <summary>Texture atlas index (for block items) or 0 (for flat items).</summary>
        public uint TexIndex;

        /// <summary>Reserved for future use.</summary>
        public uint Padding;
    }

    /// <summary>
    /// Builds vertex and index data for held items in first-person view.
    /// Block items are rendered as a 6-face cube with atlas textures.
    /// Flat items are rendered as a 2-face quad with the item sprite.
    /// </summary>
    public static class HeldItemMeshBuilder
    {
        /// <summary>Block scale in the hand (Minecraft-style 0.4 blocks).</summary>
        private const float BlockScale = 0.4f;

        /// <summary>
        /// Right hand attachment point in model space (classic arm).
        /// Matches the spec: [-6, 15, 1] scaled to block units.
        /// </summary>
        private static readonly float3 RightHandLocator = new float3(-6f, 15f, 1f) / 16f;

        /// <summary>
        /// Builds a 6-face cube for a held block item.
        /// Returns vertex/index counts. Pass null arrays to query counts only.
        /// </summary>
        public static void BuildBlockItem(
            StateRegistry stateRegistry,
            ResourceId blockId,
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

            float half = BlockScale * 0.5f;
            float3 offset = RightHandLocator;

            // Apply Minecraft firstperson_righthand display transform:
            // rotation [0, 45, 0], scale [0.4, 0.4, 0.4]
            // The rotation is baked into vertex positions.
            float yawRad = math.radians(45f);
            float cosY = math.cos(yawRad);
            float sinY = math.sin(yawRad);

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

            // Face vertex positions (before rotation)
            float3[][] faceVerts = new float3[6][];

            // +X face
            faceVerts[0] = new float3[]
            {
                new float3(half, half, -half),
                new float3(half, half, half),
                new float3(half, -half, half),
                new float3(half, -half, -half),
            };
            // -X face
            faceVerts[1] = new float3[]
            {
                new float3(-half, half, half),
                new float3(-half, half, -half),
                new float3(-half, -half, -half),
                new float3(-half, -half, half),
            };
            // +Y face
            faceVerts[2] = new float3[]
            {
                new float3(-half, half, -half),
                new float3(-half, half, half),
                new float3(half, half, half),
                new float3(half, half, -half),
            };
            // -Y face
            faceVerts[3] = new float3[]
            {
                new float3(-half, -half, half),
                new float3(-half, -half, -half),
                new float3(half, -half, -half),
                new float3(half, -half, half),
            };
            // +Z face
            faceVerts[4] = new float3[]
            {
                new float3(half, half, half),
                new float3(-half, half, half),
                new float3(-half, -half, half),
                new float3(half, -half, half),
            };
            // -Z face
            faceVerts[5] = new float3[]
            {
                new float3(-half, half, -half),
                new float3(half, half, -half),
                new float3(half, -half, -half),
                new float3(-half, -half, -half),
            };

            for (int face = 0; face < 6; face++)
            {
                float3 n = normals[face];
                // Rotate normal by 45 degrees around Y
                float3 rotN = new float3(
                    n.x * cosY + n.z * sinY,
                    n.y,
                    -n.x * sinY + n.z * cosY);

                for (int v = 0; v < 4; v++)
                {
                    float3 pos = faceVerts[face][v];

                    // Rotate position by 45 degrees around Y
                    float3 rotPos = new float3(
                        pos.x * cosY + pos.z * sinY,
                        pos.y,
                        -pos.x * sinY + pos.z * cosY);

                    rotPos += offset;

                    vertices[vertIdx + v] = new HeldItemVertex
                    {
                        Position = rotPos,
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
        /// </summary>
        public static void BuildFlatItem(
            out HeldItemVertex[] vertices,
            out int[] indices)
        {
            // 2 faces * 4 verts = 8 verts, 2 faces * 6 indices = 12 indices
            vertices = new HeldItemVertex[8];
            indices = new int[12];

            float3 offset = RightHandLocator;
            float size = 0.35f; // item sprite size in block units

            // Apply Minecraft firstperson_righthand transform for flat items:
            // rotation [0, -90, 25], scale [0.68, 0.68, 0.68]
            // Simplified: rotate -90 around Y (face toward camera), tilt 25 degrees
            float yawRad = math.radians(-90f);
            float pitchRad = math.radians(25f);

            // Front face (+Z after rotation)
            float3 n = new float3(0, 0, 1);
            float3[] quadVerts = new float3[]
            {
                new float3(-size * 0.5f, size, 0f),
                new float3(size * 0.5f, size, 0f),
                new float3(size * 0.5f, 0f, 0f),
                new float3(-size * 0.5f, 0f, 0f),
            };

            for (int v = 0; v < 4; v++)
            {
                float3 pos = quadVerts[v];
                // Rotate around Y
                float3 rotPos = new float3(
                    pos.x * math.cos(yawRad) + pos.z * math.sin(yawRad),
                    pos.y,
                    -pos.x * math.sin(yawRad) + pos.z * math.cos(yawRad));
                // Tilt
                float3 tiltPos = new float3(
                    rotPos.x,
                    rotPos.y * math.cos(pitchRad) - rotPos.z * math.sin(pitchRad),
                    rotPos.y * math.sin(pitchRad) + rotPos.z * math.cos(pitchRad));
                tiltPos += offset;

                vertices[v] = new HeldItemVertex
                {
                    Position = tiltPos,
                    Normal = n,
                    UV = GetItemQuadUV(v),
                    TexIndex = 0,
                    Padding = 0,
                };
            }

            // Back face (same verts, reversed winding, flipped normal)
            for (int v = 0; v < 4; v++)
            {
                vertices[4 + v] = vertices[v];
                vertices[4 + v].Normal = -n;
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
