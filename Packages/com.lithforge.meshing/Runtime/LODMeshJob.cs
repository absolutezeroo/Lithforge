using System.Runtime.CompilerServices;
using Lithforge.Meshing.Atlas;
using Lithforge.Voxel.Block;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lithforge.Meshing
{
    /// <summary>
    /// Simplified Burst-compiled mesh job for LOD chunks.
    /// No AO, no neighbor borders, no light data, opaque-only.
    /// Emits culled faces for downsampled voxel data.
    /// Vertex positions are scaled by the LOD voxel size.
    /// </summary>
    [BurstCompile]
    public struct LODMeshJob : IJob
    {
        [ReadOnly] public NativeArray<StateId> Data;
        [ReadOnly] public NativeArray<BlockStateCompact> StateTable;
        [ReadOnly] public NativeArray<AtlasEntry> AtlasEntries;

        /// <summary>Grid dimension (16 for LOD1, 8 for LOD2, 4 for LOD3).</summary>
        public int GridSize;

        /// <summary>World-space size of each LOD voxel (2 for LOD1, 4 for LOD2, 8 for LOD3).</summary>
        public float VoxelScale;

        public NativeList<MeshVertex> Vertices;
        public NativeList<int> Indices;

        public void Execute()
        {
            int gridSizeSq = GridSize * GridSize;

            for (int y = 0; y < GridSize; y++)
            {
                for (int z = 0; z < GridSize; z++)
                {
                    for (int x = 0; x < GridSize; x++)
                    {
                        int idx = y * gridSizeSq + z * GridSize + x;
                        StateId state = Data[idx];

                        if (state.Value == 0)
                        {
                            continue;
                        }

                        BlockStateCompact block = StateTable[state.Value];

                        if (block.IsAir)
                        {
                            continue;
                        }

                        // Check each of the 6 faces
                        CheckAndEmitFace(x, y, z, state, 0, new int3(1, 0, 0), gridSizeSq);
                        CheckAndEmitFace(x, y, z, state, 1, new int3(-1, 0, 0), gridSizeSq);
                        CheckAndEmitFace(x, y, z, state, 2, new int3(0, 1, 0), gridSizeSq);
                        CheckAndEmitFace(x, y, z, state, 3, new int3(0, -1, 0), gridSizeSq);
                        CheckAndEmitFace(x, y, z, state, 4, new int3(0, 0, 1), gridSizeSq);
                        CheckAndEmitFace(x, y, z, state, 5, new int3(0, 0, -1), gridSizeSq);
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckAndEmitFace(int x, int y, int z, StateId state, int face, int3 normal, int gridSizeSq)
        {
            int nx = x + normal.x;
            int ny = y + normal.y;
            int nz = z + normal.z;

            // Boundary faces are always visible
            if (nx >= 0 && nx < GridSize &&
                ny >= 0 && ny < GridSize &&
                nz >= 0 && nz < GridSize)
            {
                int nIdx = ny * gridSizeSq + nz * GridSize + nx;
                BlockStateCompact neighborBlock = StateTable[Data[nIdx].Value];

                if (neighborBlock.IsOpaque)
                {
                    return;
                }
            }

            EmitFace(x, y, z, state, face, (float3)normal);
        }

        private void EmitFace(int x, int y, int z, StateId state, int face, float3 normal)
        {
            int vertexStart = Vertices.Length;

            float3 basePos = new float3(x, y, z) * VoxelScale;
            float s = VoxelScale;

            AtlasEntry atlasEntry = AtlasEntries[state.Value];
            ushort texIndex = atlasEntry.GetTextureIndex(face);

            // LOD uses full brightness: AO=1, blockLight=1, sunLight=1, a=pure baseTexIndex
            half4 color = new half4((half)1.0f, (half)1.0f, (half)1.0f, (half)texIndex);

            // SHARED TINT OVERLAY PACKING — GreedyMeshJob, LODMeshJob
            byte baseTintType = atlasEntry.GetBaseTintType(face);
            ushort overlayTexIdx = atlasEntry.GetOverlayTextureIndex(face);
            byte overlayTintType = atlasEntry.GetOverlayTintType(face);
            bool hasOverlay = overlayTexIdx != 0xFFFF;

            uint tintOverlay =
                ((uint)(baseTintType & 0x3)) |
                ((uint)(overlayTintType & 0x3) << 2) |
                ((uint)(hasOverlay ? 1 : 0) << 4) |
                ((uint)(hasOverlay ? (overlayTexIdx & 0x3FF) : 0) << 5);

            float3 pos00;
            float3 pos10;
            float3 pos01;
            float3 pos11;

            switch (face)
            {
                case 0: // +X
                    pos00 = basePos + new float3(s, 0, 0);
                    pos10 = basePos + new float3(s, 0, s);
                    pos01 = basePos + new float3(s, s, 0);
                    pos11 = basePos + new float3(s, s, s);
                    break;
                case 1: // -X
                    pos00 = basePos + new float3(0, 0, s);
                    pos10 = basePos + new float3(0, 0, 0);
                    pos01 = basePos + new float3(0, s, s);
                    pos11 = basePos + new float3(0, s, 0);
                    break;
                case 2: // +Y
                    pos00 = basePos + new float3(0, s, 0);
                    pos10 = basePos + new float3(s, s, 0);
                    pos01 = basePos + new float3(0, s, s);
                    pos11 = basePos + new float3(s, s, s);
                    break;
                case 3: // -Y
                    pos00 = basePos + new float3(0, 0, s);
                    pos10 = basePos + new float3(s, 0, s);
                    pos01 = basePos + new float3(0, 0, 0);
                    pos11 = basePos + new float3(s, 0, 0);
                    break;
                case 4: // +Z
                    pos00 = basePos + new float3(s, 0, s);
                    pos10 = basePos + new float3(0, 0, s);
                    pos01 = basePos + new float3(s, s, s);
                    pos11 = basePos + new float3(0, s, s);
                    break;
                default: // -Z
                    pos00 = basePos + new float3(0, 0, 0);
                    pos10 = basePos + new float3(s, 0, 0);
                    pos01 = basePos + new float3(0, s, 0);
                    pos11 = basePos + new float3(s, s, 0);
                    break;
            }

            Vertices.Add(new MeshVertex
            {
                Position = pos00,
                Normal = normal,
                UV = new float2(0, 0),
                Color = color,
                TintOverlay = tintOverlay,
                Pad = 0,
            });

            Vertices.Add(new MeshVertex
            {
                Position = pos10,
                Normal = normal,
                UV = new float2(1, 0),
                Color = color,
                TintOverlay = tintOverlay,
                Pad = 0,
            });

            Vertices.Add(new MeshVertex
            {
                Position = pos11,
                Normal = normal,
                UV = new float2(1, 1),
                Color = color,
                TintOverlay = tintOverlay,
                Pad = 0,
            });

            Vertices.Add(new MeshVertex
            {
                Position = pos01,
                Normal = normal,
                UV = new float2(0, 1),
                Color = color,
                TintOverlay = tintOverlay,
                Pad = 0,
            });

            // CW winding for Unity front-face
            Indices.Add(vertexStart);
            Indices.Add(vertexStart + 2);
            Indices.Add(vertexStart + 1);
            Indices.Add(vertexStart);
            Indices.Add(vertexStart + 3);
            Indices.Add(vertexStart + 2);
        }
    }
}
