using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lithforge.Meshing
{
    [BurstCompile]
    public struct CulledMeshJob : IJob
    {
        [ReadOnly] public NativeArray<StateId> ChunkData;
        [ReadOnly] public NativeArray<BlockStateCompact> StateTable;
        public NativeList<MeshVertex> Vertices;
        public NativeList<int> Indices;

        public void Execute()
        {
            for (int y = 0; y < ChunkConstants.Size; y++)
            {
                for (int z = 0; z < ChunkConstants.Size; z++)
                {
                    for (int x = 0; x < ChunkConstants.Size; x++)
                    {
                        int index = Lithforge.Voxel.Chunk.ChunkData.GetIndex(x, y, z);
                        StateId current = ChunkData[index];
                        BlockStateCompact state = StateTable[current.Value];

                        if (state.IsAir)
                        {
                            continue;
                        }

                        half4 color = UnpackColor(state.MapColor);
                        float3 pos = new float3(x, y, z);

                        // +X (East)
                        if (IsFaceVisible(x + 1, y, z, 1, 0, 0))
                        {
                            EmitFace(pos, color, 0, new float3(1, 0, 0));
                        }

                        // -X (West)
                        if (IsFaceVisible(x - 1, y, z, -1, 0, 0))
                        {
                            EmitFace(pos, color, 1, new float3(-1, 0, 0));
                        }

                        // +Y (Up)
                        if (IsFaceVisible(x, y + 1, z, 0, 1, 0))
                        {
                            EmitFace(pos, color, 2, new float3(0, 1, 0));
                        }

                        // -Y (Down)
                        if (IsFaceVisible(x, y - 1, z, 0, -1, 0))
                        {
                            EmitFace(pos, color, 3, new float3(0, -1, 0));
                        }

                        // +Z (South)
                        if (IsFaceVisible(x, y, z + 1, 0, 0, 1))
                        {
                            EmitFace(pos, color, 4, new float3(0, 0, 1));
                        }

                        // -Z (North)
                        if (IsFaceVisible(x, y, z - 1, 0, 0, -1))
                        {
                            EmitFace(pos, color, 5, new float3(0, 0, -1));
                        }
                    }
                }
            }
        }

        private bool IsFaceVisible(int nx, int ny, int nz, int dx, int dy, int dz)
        {
            if (nx < 0 || nx >= ChunkConstants.Size ||
                ny < 0 || ny >= ChunkConstants.Size ||
                nz < 0 || nz >= ChunkConstants.Size)
            {
                return true; // Treat out-of-bounds as air
            }

            int neighborIndex = Lithforge.Voxel.Chunk.ChunkData.GetIndex(nx, ny, nz);
            StateId neighborId = ChunkData[neighborIndex];
            BlockStateCompact neighborState = StateTable[neighborId.Value];

            return !neighborState.IsOpaque;
        }

        private void EmitFace(float3 pos, half4 color, int faceIndex, float3 normal)
        {
            int vertexStart = Vertices.Length;

            float3 v0, v1, v2, v3;
            GetFaceVertices(pos, faceIndex, out v0, out v1, out v2, out v3);

            MeshVertex vert0 = new MeshVertex
            {
                Position = v0,
                Normal = normal,
                UV = new float2(0, 0),
                Color = color,
                TintOverlay = 0,
                Pad = 0,
            };

            MeshVertex vert1 = new MeshVertex
            {
                Position = v1,
                Normal = normal,
                UV = new float2(1, 0),
                Color = color,
                TintOverlay = 0,
                Pad = 0,
            };

            MeshVertex vert2 = new MeshVertex
            {
                Position = v2,
                Normal = normal,
                UV = new float2(1, 1),
                Color = color,
                TintOverlay = 0,
                Pad = 0,
            };

            MeshVertex vert3 = new MeshVertex
            {
                Position = v3,
                Normal = normal,
                UV = new float2(0, 1),
                Color = color,
                TintOverlay = 0,
                Pad = 0,
            };

            Vertices.Add(vert0);
            Vertices.Add(vert1);
            Vertices.Add(vert2);
            Vertices.Add(vert3);

            Indices.Add(vertexStart);
            Indices.Add(vertexStart + 1);
            Indices.Add(vertexStart + 2);
            Indices.Add(vertexStart);
            Indices.Add(vertexStart + 2);
            Indices.Add(vertexStart + 3);
        }

        private static void GetFaceVertices(float3 pos, int faceIndex,
            out float3 v0, out float3 v1, out float3 v2, out float3 v3)
        {
            switch (faceIndex)
            {
                case 0: // +X (East)
                    v0 = pos + new float3(1, 0, 0);
                    v1 = pos + new float3(1, 0, 1);
                    v2 = pos + new float3(1, 1, 1);
                    v3 = pos + new float3(1, 1, 0);
                    return;
                case 1: // -X (West)
                    v0 = pos + new float3(0, 0, 1);
                    v1 = pos + new float3(0, 0, 0);
                    v2 = pos + new float3(0, 1, 0);
                    v3 = pos + new float3(0, 1, 1);
                    return;
                case 2: // +Y (Up)
                    v0 = pos + new float3(0, 1, 0);
                    v1 = pos + new float3(1, 1, 0);
                    v2 = pos + new float3(1, 1, 1);
                    v3 = pos + new float3(0, 1, 1);
                    return;
                case 3: // -Y (Down)
                    v0 = pos + new float3(0, 0, 1);
                    v1 = pos + new float3(1, 0, 1);
                    v2 = pos + new float3(1, 0, 0);
                    v3 = pos + new float3(0, 0, 0);
                    return;
                case 4: // +Z (South)
                    v0 = pos + new float3(1, 0, 1);
                    v1 = pos + new float3(0, 0, 1);
                    v2 = pos + new float3(0, 1, 1);
                    v3 = pos + new float3(1, 1, 1);
                    return;
                default: // -Z (North)
                    v0 = pos + new float3(0, 0, 0);
                    v1 = pos + new float3(1, 0, 0);
                    v2 = pos + new float3(1, 1, 0);
                    v3 = pos + new float3(0, 1, 0);
                    return;
            }
        }

        private static half4 UnpackColor(uint packed)
        {
            float r = ((packed >> 24) & 0xFF) / 255.0f;
            float g = ((packed >> 16) & 0xFF) / 255.0f;
            float b = ((packed >> 8) & 0xFF) / 255.0f;
            float a = (packed & 0xFF) / 255.0f;

            return new half4((half)r, (half)g, (half)b, (half)a);
        }
    }
}
