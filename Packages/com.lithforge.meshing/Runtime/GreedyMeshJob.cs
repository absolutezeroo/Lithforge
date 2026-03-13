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
    /// Burst-compiled binary greedy meshing job with ambient occlusion.
    /// Processes 6 face directions, each with 32 slices. For each slice,
    /// builds a face visibility mask, computes per-vertex AO, and performs
    /// greedy merging of identical adjacent faces.
    ///
    /// Neighbor border slices enable correct cross-chunk face culling.
    /// </summary>
    [BurstCompile]
    public struct GreedyMeshJob : IJob
    {
        [ReadOnly] public NativeArray<StateId> ChunkData;
        [ReadOnly] public NativeArray<StateId> NeighborPosX;
        [ReadOnly] public NativeArray<StateId> NeighborNegX;
        [ReadOnly] public NativeArray<StateId> NeighborPosY;
        [ReadOnly] public NativeArray<StateId> NeighborNegY;
        [ReadOnly] public NativeArray<StateId> NeighborPosZ;
        [ReadOnly] public NativeArray<StateId> NeighborNegZ;
        [ReadOnly] public NativeArray<BlockStateCompact> StateTable;
        [ReadOnly] public NativeArray<AtlasEntry> AtlasEntries;
        [ReadOnly] public NativeArray<byte> LightData;

        public NativeList<MeshVertex> OpaqueVertices;
        public NativeList<int> OpaqueIndices;
        public NativeList<MeshVertex> CutoutVertices;
        public NativeList<int> CutoutIndices;
        public NativeList<MeshVertex> TranslucentVertices;
        public NativeList<int> TranslucentIndices;

        public void Execute()
        {
            // Process each of the 6 face directions
            for (int face = 0; face < 6; face++)
            {
                ProcessFaceDirection(face);
            }
        }

        private void ProcessFaceDirection(int face)
        {
            NativeArray<uint> rowMask = new NativeArray<uint>(ChunkConstants.Size, Allocator.Temp);
            NativeArray<ushort> faceStateId = new NativeArray<ushort>(ChunkConstants.SizeSquared, Allocator.Temp);
            NativeArray<byte> faceRenderLayer = new NativeArray<byte>(ChunkConstants.SizeSquared, Allocator.Temp);
            NativeArray<byte> faceAO00 = new NativeArray<byte>(ChunkConstants.SizeSquared, Allocator.Temp);
            NativeArray<byte> faceAO10 = new NativeArray<byte>(ChunkConstants.SizeSquared, Allocator.Temp);
            NativeArray<byte> faceAO01 = new NativeArray<byte>(ChunkConstants.SizeSquared, Allocator.Temp);
            NativeArray<byte> faceAO11 = new NativeArray<byte>(ChunkConstants.SizeSquared, Allocator.Temp);
            NativeArray<byte> faceLight = new NativeArray<byte>(ChunkConstants.SizeSquared, Allocator.Temp);

            for (int slice = 0; slice < ChunkConstants.Size; slice++)
            {
                // Clear masks for this slice
                for (int i = 0; i < ChunkConstants.Size; i++)
                {
                    rowMask[i] = 0;
                }

                // Build face visibility mask and compute AO
                BuildFaceMask(face, slice, rowMask, faceStateId, faceRenderLayer, faceAO00, faceAO10, faceAO01, faceAO11, faceLight);

                // Greedy merge and emit quads
                GreedyMerge(face, slice, rowMask, faceStateId, faceRenderLayer, faceAO00, faceAO10, faceAO01, faceAO11, faceLight);
            }

            rowMask.Dispose();
            faceStateId.Dispose();
            faceRenderLayer.Dispose();
            faceAO00.Dispose();
            faceAO10.Dispose();
            faceAO01.Dispose();
            faceAO11.Dispose();
            faceLight.Dispose();
        }

        private void BuildFaceMask(
            int face, int slice,
            NativeArray<uint> rowMask,
            NativeArray<ushort> faceStateId,
            NativeArray<byte> faceRenderLayer,
            NativeArray<byte> faceAO00, NativeArray<byte> faceAO10,
            NativeArray<byte> faceAO01, NativeArray<byte> faceAO11,
            NativeArray<byte> faceLight)
        {
            for (int v = 0; v < ChunkConstants.Size; v++)
            {
                for (int u = 0; u < ChunkConstants.Size; u++)
                {
                    int3 blockPos = FaceToBlockPos(face, slice, u, v);
                    int3 neighborPos = blockPos + GetFaceNormal(face);

                    StateId blockId = SampleBlock(blockPos);
                    StateId neighborId = SampleBlock(neighborPos);

                    BlockStateCompact blockState = StateTable[blockId.Value];
                    BlockStateCompact neighborState = StateTable[neighborId.Value];

                    // Determine if this face should be rendered:
                    // - Opaque blocks: render face when neighbor is not opaque
                    // - Translucent blocks (water, glass): render face when neighbor is
                    //   air or a different block type (skip same-type internal faces)
                    bool shouldRender = false;

                    if (!blockState.IsAir)
                    {
                        if (blockState.IsOpaque)
                        {
                            shouldRender = !neighborState.IsOpaque;
                        }
                        else
                        {
                            shouldRender = !neighborState.IsOpaque && neighborId.Value != blockId.Value;
                        }
                    }

                    if (shouldRender)
                    {
                        int idx = v * ChunkConstants.Size + u;
                        rowMask[v] = rowMask[v] | (1u << u);
                        faceStateId[idx] = blockId.Value;
                        faceRenderLayer[idx] = blockState.RenderLayer;

                        // Compute AO for each vertex corner
                        ComputeVertexAO(face, blockPos, u, v,
                            out byte ao00, out byte ao10, out byte ao01, out byte ao11);

                        faceAO00[idx] = ao00;
                        faceAO10[idx] = ao10;
                        faceAO01[idx] = ao01;
                        faceAO11[idx] = ao11;

                        // Sample light at the exposed side (air/transparent neighbor)
                        faceLight[idx] = SampleLight(neighborPos);
                    }
                }
            }
        }

        private void GreedyMerge(
            int face, int slice,
            NativeArray<uint> rowMask,
            NativeArray<ushort> faceStateId,
            NativeArray<byte> faceRenderLayer,
            NativeArray<byte> faceAO00, NativeArray<byte> faceAO10,
            NativeArray<byte> faceAO01, NativeArray<byte> faceAO11,
            NativeArray<byte> faceLight)
        {
            for (int v = 0; v < ChunkConstants.Size; v++)
            {
                uint mask = rowMask[v];

                while (mask != 0)
                {
                    int u0 = math.tzcnt(mask);
                    int idx0 = v * ChunkConstants.Size + u0;
                    ushort stateVal = faceStateId[idx0];
                    byte renderLayer = faceRenderLayer[idx0];
                    byte ao00 = faceAO00[idx0];
                    byte ao10 = faceAO10[idx0];
                    byte ao01 = faceAO01[idx0];
                    byte ao11 = faceAO11[idx0];
                    byte light = faceLight[idx0];

                    // Extend width
                    int width = 1;

                    while (u0 + width < ChunkConstants.Size)
                    {
                        if ((mask & (1u << (u0 + width))) == 0)
                        {
                            break;
                        }

                        int idxW = v * ChunkConstants.Size + u0 + width;

                        if (faceStateId[idxW] != stateVal ||
                            faceRenderLayer[idxW] != renderLayer ||
                            faceAO00[idxW] != ao00 || faceAO10[idxW] != ao10 ||
                            faceAO01[idxW] != ao01 || faceAO11[idxW] != ao11 ||
                            (faceLight[idxW] >> 2) != (light >> 2))
                        {
                            break;
                        }

                        width++;
                    }

                    // Extend height
                    int height = 1;

                    while (v + height < ChunkConstants.Size)
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

                            int idxH = (v + height) * ChunkConstants.Size + u0 + wu;

                            if (faceStateId[idxH] != stateVal ||
                                faceRenderLayer[idxH] != renderLayer ||
                                faceAO00[idxH] != ao00 || faceAO10[idxH] != ao10 ||
                                faceAO01[idxH] != ao01 || faceAO11[idxH] != ao11 ||
                                (faceLight[idxH] >> 2) != (light >> 2))
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

                    // Emit the greedy quad
                    EmitGreedyQuad(face, slice, u0, v, width, height, stateVal,
                        ao00, ao10, ao01, ao11, light, renderLayer);

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

                    // Refresh current row mask
                    mask = rowMask[v];
                }
            }
        }

        private void EmitGreedyQuad(
            int face, int slice, int u0, int v0, int width, int height,
            ushort stateVal, byte ao00, byte ao10, byte ao01, byte ao11, byte light,
            byte renderLayer)
        {
            // Route to correct buffer: renderLayer 0=opaque, 1=cutout, 2=translucent
            NativeList<MeshVertex> targetVertices;
            NativeList<int> targetIndices;

            (targetVertices, targetIndices) = renderLayer switch
            {
                1 => (CutoutVertices, CutoutIndices),
                2 => (TranslucentVertices, TranslucentIndices),
                _ => (OpaqueVertices, OpaqueIndices),
            };

            int vertexStart = targetVertices.Length;

            ComputeQuadPositions(face, slice, u0, v0, width, height, stateVal,
                out float3 pos00, out float3 pos10, out float3 pos01, out float3 pos11);

            float3 normal = (float3)GetFaceNormal(face);

            AtlasEntry atlasEntry = AtlasEntries[stateVal];
            ushort texIndex = atlasEntry.GetTextureIndex(face);

            // Unpack nibbles: high 4 bits = sunLight, low 4 bits = blockLight
            // Minecraft gamma curve: brightness = pow(0.8, 15 - level)
            byte sunLight = (byte)(light >> 4);
            byte blockLight = (byte)(light & 0x0F);
            float sunNorm = math.pow(0.8f, 15 - sunLight);
            float blockNorm = math.pow(0.8f, 15 - blockLight);

            // Vertex color: r=AO, g=blockLight, b=sunLight, a=pure baseTexIndex
            half4 color00 = new half4((half)(ao00 / 3.0f), (half)blockNorm, (half)sunNorm, (half)texIndex);
            half4 color10 = new half4((half)(ao10 / 3.0f), (half)blockNorm, (half)sunNorm, (half)texIndex);
            half4 color01 = new half4((half)(ao01 / 3.0f), (half)blockNorm, (half)sunNorm, (half)texIndex);
            half4 color11 = new half4((half)(ao11 / 3.0f), (half)blockNorm, (half)sunNorm, (half)texIndex);

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

            targetVertices.Add(new MeshVertex
            {
                Position = pos00,
                Normal = normal,
                UV = new float2(0, 0),
                Color = color00,
                TintOverlay = tintOverlay,
                Pad = 0,
            });

            targetVertices.Add(new MeshVertex
            {
                Position = pos10,
                Normal = normal,
                UV = new float2(width, 0),
                Color = color10,
                TintOverlay = tintOverlay,
                Pad = 0,
            });

            targetVertices.Add(new MeshVertex
            {
                Position = pos11,
                Normal = normal,
                UV = new float2(width, height),
                Color = color11,
                TintOverlay = tintOverlay,
                Pad = 0,
            });

            targetVertices.Add(new MeshVertex
            {
                Position = pos01,
                Normal = normal,
                UV = new float2(0, height),
                Color = color01,
                TintOverlay = tintOverlay,
                Pad = 0,
            });

            // AO diagonal flip: use the diagonal that minimizes anisotropy.
            // When ao00+ao11 > ao10+ao01, the 00-11 diagonal has more contrast,
            // so we flip to the 10-01 diagonal for smoother interpolation.
            if (ao00 + ao11 > ao10 + ao01)
            {
                // Flipped winding: use 10-01 diagonal (CW for Unity front-face)
                targetIndices.Add(vertexStart + 1);
                targetIndices.Add(vertexStart + 3);
                targetIndices.Add(vertexStart + 2);
                targetIndices.Add(vertexStart + 1);
                targetIndices.Add(vertexStart);
                targetIndices.Add(vertexStart + 3);
            }
            else
            {
                // Standard winding: use 00-11 diagonal (CW for Unity front-face)
                targetIndices.Add(vertexStart);
                targetIndices.Add(vertexStart + 2);
                targetIndices.Add(vertexStart + 1);
                targetIndices.Add(vertexStart);
                targetIndices.Add(vertexStart + 3);
                targetIndices.Add(vertexStart + 2);
            }
        }

        private void ComputeVertexAO(int face, int3 blockPos, int u, int v,
            out byte ao00, out byte ao10, out byte ao01, out byte ao11)
        {
            // The 4 corners of the face cell each sample 3 neighbors for AO
            // Corner (0,0) = bottom-left, (1,0) = bottom-right, (0,1) = top-left, (1,1) = top-right
            // Relative to the face plane's u,v axes

            int3 faceNormal = GetFaceNormal(face);
            int3 uAxis = GetFaceUAxis(face);
            int3 vAxis = GetFaceVAxis(face);

            // Position one step into the face direction (where the face is rendered)
            int3 faceBase = blockPos + faceNormal;

            // Shared axis queries (each used by 2 corners)
            bool sNegU = IsBlockOpaqueForAO(faceBase - uAxis);
            bool sPosU = IsBlockOpaqueForAO(faceBase + uAxis);
            bool sNegV = IsBlockOpaqueForAO(faceBase - vAxis);
            bool sPosV = IsBlockOpaqueForAO(faceBase + vAxis);

            ao00 = VoxelAO.Compute(sNegU, sNegV, IsBlockOpaqueForAO(faceBase - uAxis - vAxis));
            ao10 = VoxelAO.Compute(sPosU, sNegV, IsBlockOpaqueForAO(faceBase + uAxis - vAxis));
            ao01 = VoxelAO.Compute(sNegU, sPosV, IsBlockOpaqueForAO(faceBase - uAxis + vAxis));
            ao11 = VoxelAO.Compute(sPosU, sPosV, IsBlockOpaqueForAO(faceBase + uAxis + vAxis));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsBlockOpaqueForAO(int3 pos)
        {
            StateId id = SampleBlock(pos);
            return StateTable[id.Value].IsOpaque;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private StateId SampleBlock(int3 pos)
        {
            // Inside chunk bounds
            if (pos.x >= 0 && pos.x < ChunkConstants.Size &&
                pos.y >= 0 && pos.y < ChunkConstants.Size &&
                pos.z >= 0 && pos.z < ChunkConstants.Size)
            {
                return ChunkData[Lithforge.Voxel.Chunk.ChunkData.GetIndex(pos.x, pos.y, pos.z)];
            }

            // Multi-axis out-of-bounds (diagonal/corner) — treat as air
            bool outX = pos.x < 0 || pos.x >= ChunkConstants.Size;
            bool outY = pos.y < 0 || pos.y >= ChunkConstants.Size;
            bool outZ = pos.z < 0 || pos.z >= ChunkConstants.Size;

            if ((outX ? 1 : 0) + (outY ? 1 : 0) + (outZ ? 1 : 0) > 1)
            {
                return StateId.Air;
            }

            // Outside chunk on single axis — sample from neighbor border slices
            if (pos.x >= ChunkConstants.Size)
            {
                int nz = math.clamp(pos.z, 0, ChunkConstants.Size - 1);
                int ny = math.clamp(pos.y, 0, ChunkConstants.Size - 1);

                return NeighborPosX[ny * ChunkConstants.Size + nz];
            }

            if (pos.x < 0)
            {
                int nz = math.clamp(pos.z, 0, ChunkConstants.Size - 1);
                int ny = math.clamp(pos.y, 0, ChunkConstants.Size - 1);

                return NeighborNegX[ny * ChunkConstants.Size + nz];
            }

            if (pos.y >= ChunkConstants.Size)
            {
                int nx = math.clamp(pos.x, 0, ChunkConstants.Size - 1);
                int nz = math.clamp(pos.z, 0, ChunkConstants.Size - 1);

                return NeighborPosY[nz * ChunkConstants.Size + nx];
            }

            if (pos.y < 0)
            {
                int nx = math.clamp(pos.x, 0, ChunkConstants.Size - 1);
                int nz = math.clamp(pos.z, 0, ChunkConstants.Size - 1);

                return NeighborNegY[nz * ChunkConstants.Size + nx];
            }
            if (pos.z >= ChunkConstants.Size)
            {
                int nx = math.clamp(pos.x, 0, ChunkConstants.Size - 1);
                int ny = math.clamp(pos.y, 0, ChunkConstants.Size - 1);

                return NeighborPosZ[ny * ChunkConstants.Size + nx];
            }

            if (pos.z < 0)
            {
                int nx = math.clamp(pos.x, 0, ChunkConstants.Size - 1);
                int ny = math.clamp(pos.y, 0, ChunkConstants.Size - 1);

                return NeighborNegZ[ny * ChunkConstants.Size + nx];
            }

            return StateId.Air;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte SampleLight(int3 pos)
        {
            if (!LightData.IsCreated || LightData.Length == 0)
            {
                // 0xF0 = sun=15, block=0 (full sunlight, no block light)
                return 240;
            }

            if (pos.x >= 0 && pos.x < ChunkConstants.Size &&
                pos.y >= 0 && pos.y < ChunkConstants.Size &&
                pos.z >= 0 && pos.z < ChunkConstants.Size)
            {
                return LightData[Lithforge.Voxel.Chunk.ChunkData.GetIndex(pos.x, pos.y, pos.z)];
            }

            // 0xF0 = sun=15, block=0 (full sunlight, no block light)
            return 240;
        }

        /// <summary>
        /// Converts face direction + slice + (u,v) to block-local (x,y,z).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int3 FaceToBlockPos(int face, int slice, int u, int v)
        {
            return face switch
            {
                // +X: slice=x, u=z, v=y
                0 => new int3(slice, v, u),
                // -X: slice=x, u=z, v=y
                1 => new int3(slice, v, u),
                // +Y: slice=y, u=x, v=z
                2 => new int3(u, slice, v),
                // -Y: slice=y, u=x, v=z
                3 => new int3(u, slice, v),
                // +Z: slice=z, u=x, v=y
                4 => new int3(u, v, slice),
                // -Z: slice=z, u=x, v=y
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int3 GetFaceUAxis(int face)
        {
            return face switch
            {
                // +X: u=z
                0 => new int3(0, 0, 1),
                // -X: u=z
                1 => new int3(0, 0, 1),
                // +Y: u=x
                2 => new int3(1, 0, 0),
                // -Y: u=x
                3 => new int3(1, 0, 0),
                // +Z: u=x
                4 => new int3(1, 0, 0),
                // -Z: u=x
                _ => new int3(1, 0, 0),
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int3 GetFaceVAxis(int face)
        {
            return face switch
            {
                // +X: v=y
                0 => new int3(0, 1, 0),
                // -X: v=y
                1 => new int3(0, 1, 0),
                // +Y: v=z
                2 => new int3(0, 0, 1),
                // -Y: v=z
                3 => new int3(0, 0, 1),
                // +Z: v=y
                4 => new int3(0, 1, 0),
                _ => new int3(0, 1, 0),
            };
        }

        private void ComputeQuadPositions(
            int face, int slice, int u0, int v0, int width, int height,
            ushort stateVal,
            out float3 pos00, out float3 pos10, out float3 pos01, out float3 pos11)
        {
            // The quad is emitted on the face of the block at the slice position.
            // For positive faces (+X, +Y, +Z), the face is at slice+1.
            // For negative faces (-X, -Y, -Z), the face is at slice.
            // Fluid blocks have their top face lowered by 2/16 (0.125).
            bool isFluidTop = face == 2 && StateTable[stateVal].IsFluid;
            float fluidOffset = isFluidTop ? -0.125f : 0.0f;
            float sliceOffset = (face == 0 || face == 2 || face == 4)
                ? (slice + 1.0f + fluidOffset)
                : slice;

            switch (face)
            {
                case 0: // +X: face at x=slice+1, quad spans z=[u0..u0+w], y=[v0..v0+h]
                    pos00 = new float3(sliceOffset, v0, u0);
                    pos10 = new float3(sliceOffset, v0, u0 + width);
                    pos01 = new float3(sliceOffset, v0 + height, u0);
                    pos11 = new float3(sliceOffset, v0 + height, u0 + width);

                    return;
                case 1: // -X: face at x=slice, quad spans z=[u0..u0+w], y=[v0..v0+h]
                    pos00 = new float3(sliceOffset, v0, u0 + width);
                    pos10 = new float3(sliceOffset, v0, u0);
                    pos01 = new float3(sliceOffset, v0 + height, u0 + width);
                    pos11 = new float3(sliceOffset, v0 + height, u0);

                    return;
                case 2: // +Y: face at y=slice+1, quad spans x=[u0..u0+w], z=[v0..v0+h]
                    pos00 = new float3(u0, sliceOffset, v0);
                    pos10 = new float3(u0 + width, sliceOffset, v0);
                    pos01 = new float3(u0, sliceOffset, v0 + height);
                    pos11 = new float3(u0 + width, sliceOffset, v0 + height);

                    return;
                case 3: // -Y: face at y=slice, quad spans x=[u0..u0+w], z=[v0..v0+h]
                    pos00 = new float3(u0, sliceOffset, v0 + height);
                    pos10 = new float3(u0 + width, sliceOffset, v0 + height);
                    pos01 = new float3(u0, sliceOffset, v0);
                    pos11 = new float3(u0 + width, sliceOffset, v0);

                    return;
                case 4: // +Z: face at z=slice+1, quad spans x=[u0..u0+w], y=[v0..v0+h]
                    pos00 = new float3(u0 + width, v0, sliceOffset);
                    pos10 = new float3(u0, v0, sliceOffset);
                    pos01 = new float3(u0 + width, v0 + height, sliceOffset);
                    pos11 = new float3(u0, v0 + height, sliceOffset);

                    return;
                default: // -Z: face at z=slice, quad spans x=[u0..u0+w], y=[v0..v0+h]
                    pos00 = new float3(u0, v0, sliceOffset);
                    pos10 = new float3(u0 + width, v0, sliceOffset);
                    pos01 = new float3(u0, v0 + height, sliceOffset);
                    pos11 = new float3(u0 + width, v0 + height, sliceOffset);

                    return;
            }
        }
    }
}
