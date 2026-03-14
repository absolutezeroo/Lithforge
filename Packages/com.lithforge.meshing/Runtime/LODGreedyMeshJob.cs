using System.Runtime.CompilerServices;
using Lithforge.Meshing.Atlas;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lithforge.Meshing
{
    /// <summary>
    /// Burst-compiled greedy meshing job for LOD chunks.
    /// Simplified version of GreedyMeshJob: no AO, no neighbor borders, no light data,
    /// opaque-only output. Uses binary greedy merge for large surface reduction.
    /// Variable grid size (16 for LOD1, 8 for LOD2, 4 for LOD3).
    /// Emits PackedMeshVertex with full brightness (AO=3, light=15/15).
    /// </summary>
    [BurstCompile]
    public struct LODGreedyMeshJob : IJob
    {
        [ReadOnly] public NativeArray<StateId> Data;
        [ReadOnly] public NativeArray<BlockStateCompact> StateTable;
        [ReadOnly] public NativeArray<AtlasEntry> AtlasEntries;

        /// <summary>Grid dimension (16 for LOD1, 8 for LOD2, 4 for LOD3).</summary>
        public int GridSize;

        /// <summary>LOD scale index for packed vertex (0=x1, 1=x2, 2=x4, 3=x8).</summary>
        public int LODScaleIndex;

        /// <summary>Chunk coordinate for world position encoding in packed vertex.</summary>
        public int3 ChunkCoord;

        public NativeList<PackedMeshVertex> Vertices;
        public NativeList<int> Indices;

        public void Execute()
        {
            for (int face = 0; face < 6; face++)
            {
                ProcessFaceDirection(face);
            }
        }

        private void ProcessFaceDirection(int face)
        {
            NativeArray<uint> rowMask = new NativeArray<uint>(GridSize, Allocator.Temp);
            NativeArray<ushort> faceStateId = new NativeArray<ushort>(GridSize * GridSize, Allocator.Temp);

            for (int slice = 0; slice < GridSize; slice++)
            {
                for (int i = 0; i < GridSize; i++)
                {
                    rowMask[i] = 0;
                }

                BuildFaceMask(face, slice, rowMask, faceStateId);
                GreedyMerge(face, slice, rowMask, faceStateId);
            }

            rowMask.Dispose();
            faceStateId.Dispose();
        }

        private void BuildFaceMask(
            int face, int slice,
            NativeArray<uint> rowMask,
            NativeArray<ushort> faceStateId)
        {
            int gridSizeSq = GridSize * GridSize;

            for (int v = 0; v < GridSize; v++)
            {
                for (int u = 0; u < GridSize; u++)
                {
                    int3 blockPos = FaceToBlockPos(face, slice, u, v);
                    int3 neighborPos = blockPos + GetFaceNormal(face);

                    StateId blockId = SampleBlock(blockPos, gridSizeSq);
                    BlockStateCompact blockState = StateTable[blockId.Value];

                    if (blockState.IsAir)
                    {
                        continue;
                    }

                    // Determine visibility: face visible if neighbor is not opaque
                    StateId neighborId = SampleBlock(neighborPos, gridSizeSq);
                    BlockStateCompact neighborState = StateTable[neighborId.Value];

                    if (neighborState.IsOpaque)
                    {
                        continue;
                    }

                    int idx = v * GridSize + u;
                    rowMask[v] = rowMask[v] | (1u << u);
                    faceStateId[idx] = blockId.Value;
                }
            }
        }

        private void GreedyMerge(
            int face, int slice,
            NativeArray<uint> rowMask,
            NativeArray<ushort> faceStateId)
        {
            for (int v = 0; v < GridSize; v++)
            {
                uint mask = rowMask[v];

                while (mask != 0)
                {
                    int u0 = math.tzcnt(mask);
                    int idx0 = v * GridSize + u0;
                    ushort stateVal = faceStateId[idx0];

                    // Extend width
                    int width = 1;

                    while (u0 + width < GridSize)
                    {
                        if ((mask & (1u << (u0 + width))) == 0)
                        {
                            break;
                        }

                        int idxW = v * GridSize + u0 + width;

                        if (faceStateId[idxW] != stateVal)
                        {
                            break;
                        }

                        width++;
                    }

                    // Extend height
                    int height = 1;

                    while (v + height < GridSize)
                    {
                        bool canExtend = true;

                        for (int wu = 0; wu < width; wu++)
                        {
                            uint nextRowMask = rowMask[v + height];

                            if ((nextRowMask & (1u << (u0 + wu))) == 0)
                            {
                                canExtend = false;

                                break;
                            }

                            int idxH = (v + height) * GridSize + u0 + wu;

                            if (faceStateId[idxH] != stateVal)
                            {
                                canExtend = false;

                                break;
                            }
                        }

                        if (!canExtend)
                        {
                            break;
                        }

                        height++;
                    }

                    EmitGreedyQuad(face, slice, u0, v, width, height, stateVal);

                    // Clear consumed bits
                    uint clearMask = 0u;

                    for (int wu = 0; wu < width; wu++)
                    {
                        clearMask |= (1u << (u0 + wu));
                    }

                    for (int hh = 0; hh < height; hh++)
                    {
                        rowMask[v + hh] = rowMask[v + hh] & ~clearMask;
                    }

                    mask = rowMask[v];
                }
            }
        }

        private void EmitGreedyQuad(
            int face, int slice, int u0, int v0, int width, int height,
            ushort stateVal)
        {
            int vertexStart = Vertices.Length;

            AtlasEntry atlasEntry = AtlasEntries[stateVal];
            ushort texIndex = atlasEntry.GetTextureIndex(face);
            byte baseTintType = atlasEntry.GetBaseTintType(face);
            ushort overlayTexIdx = atlasEntry.GetOverlayTextureIndex(face);
            byte overlayTintType = atlasEntry.GetOverlayTintType(face);
            bool hasOverlay = overlayTexIdx != 0xFFFF;

            int cwx = ChunkCoord.x * ChunkConstants.Size;
            int cwy = ChunkCoord.y * ChunkConstants.Size;
            int cwz = ChunkCoord.z * ChunkConstants.Size;

            // LOD: full brightness, no AO
            int ao = 3;
            int blockLight = 15;
            int sunLight = 15;

            // Compute integer positions for each corner based on face direction
            // sliceOffset = slice+1 for positive faces, slice for negative faces
            int sliceOff = (face == 0 || face == 2 || face == 4) ? slice + 1 : slice;

            int px00;
            int py00;
            int pz00;
            int px10;
            int py10;
            int pz10;
            int px11;
            int py11;
            int pz11;
            int px01;
            int py01;
            int pz01;

            switch (face)
            {
                case 0: // +X
                    px00 = sliceOff; py00 = v0;          pz00 = u0;
                    px10 = sliceOff; py10 = v0;          pz10 = u0 + width;
                    px11 = sliceOff; py11 = v0 + height; pz11 = u0 + width;
                    px01 = sliceOff; py01 = v0 + height; pz01 = u0;
                    break;
                case 1: // -X
                    px00 = sliceOff; py00 = v0;          pz00 = u0 + width;
                    px10 = sliceOff; py10 = v0;          pz10 = u0;
                    px11 = sliceOff; py11 = v0 + height; pz11 = u0;
                    px01 = sliceOff; py01 = v0 + height; pz01 = u0 + width;
                    break;
                case 2: // +Y
                    px00 = u0;         py00 = sliceOff; pz00 = v0;
                    px10 = u0 + width; py10 = sliceOff; pz10 = v0;
                    px11 = u0 + width; py11 = sliceOff; pz11 = v0 + height;
                    px01 = u0;         py01 = sliceOff; pz01 = v0 + height;
                    break;
                case 3: // -Y
                    px00 = u0;         py00 = sliceOff; pz00 = v0 + height;
                    px10 = u0 + width; py10 = sliceOff; pz10 = v0 + height;
                    px11 = u0 + width; py11 = sliceOff; pz11 = v0;
                    px01 = u0;         py01 = sliceOff; pz01 = v0;
                    break;
                case 4: // +Z
                    px00 = u0 + width; py00 = v0;          pz00 = sliceOff;
                    px10 = u0;         py10 = v0;          pz10 = sliceOff;
                    px11 = u0;         py11 = v0 + height; pz11 = sliceOff;
                    px01 = u0 + width; py01 = v0 + height; pz01 = sliceOff;
                    break;
                default: // -Z
                    px00 = u0;         py00 = v0;          pz00 = sliceOff;
                    px10 = u0 + width; py10 = v0;          pz10 = sliceOff;
                    px11 = u0 + width; py11 = v0 + height; pz11 = sliceOff;
                    px01 = u0;         py01 = v0 + height; pz01 = sliceOff;
                    break;
            }

            Vertices.Add(PackedMeshVertex.Pack(
                px00, py00, pz00, face, ao, blockLight, sunLight, false,
                texIndex, baseTintType, hasOverlay,
                hasOverlay ? overlayTexIdx : 0, overlayTintType,
                LODScaleIndex, 0, 0, cwx, cwy, cwz));

            Vertices.Add(PackedMeshVertex.Pack(
                px10, py10, pz10, face, ao, blockLight, sunLight, false,
                texIndex, baseTintType, hasOverlay,
                hasOverlay ? overlayTexIdx : 0, overlayTintType,
                LODScaleIndex, width, 0, cwx, cwy, cwz));

            Vertices.Add(PackedMeshVertex.Pack(
                px11, py11, pz11, face, ao, blockLight, sunLight, false,
                texIndex, baseTintType, hasOverlay,
                hasOverlay ? overlayTexIdx : 0, overlayTintType,
                LODScaleIndex, width, height, cwx, cwy, cwz));

            Vertices.Add(PackedMeshVertex.Pack(
                px01, py01, pz01, face, ao, blockLight, sunLight, false,
                texIndex, baseTintType, hasOverlay,
                hasOverlay ? overlayTexIdx : 0, overlayTintType,
                LODScaleIndex, 0, height, cwx, cwy, cwz));

            // Standard winding (no AO diagonal flip for LOD — AO is uniform)
            Indices.Add(vertexStart);
            Indices.Add(vertexStart + 2);
            Indices.Add(vertexStart + 1);
            Indices.Add(vertexStart);
            Indices.Add(vertexStart + 3);
            Indices.Add(vertexStart + 2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private StateId SampleBlock(int3 pos, int gridSizeSq)
        {
            if (pos.x < 0 || pos.x >= GridSize ||
                pos.y < 0 || pos.y >= GridSize ||
                pos.z < 0 || pos.z >= GridSize)
            {
                return StateId.Air;
            }

            return Data[pos.y * gridSizeSq + pos.z * GridSize + pos.x];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int3 FaceToBlockPos(int face, int slice, int u, int v)
        {
            return face switch
            {
                0 => new int3(slice, v, u),
                1 => new int3(slice, v, u),
                2 => new int3(u, slice, v),
                3 => new int3(u, slice, v),
                4 => new int3(u, v, slice),
                _ => new int3(u, v, slice),
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int3 GetFaceNormal(int face)
        {
            return face switch
            {
                0 => new int3(1, 0, 0),
                1 => new int3(-1, 0, 0),
                2 => new int3(0, 1, 0),
                3 => new int3(0, -1, 0),
                4 => new int3(0, 0, 1),
                _ => new int3(0, 0, -1),
            };
        }
    }
}
