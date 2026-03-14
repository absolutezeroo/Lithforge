# Lithforge — Meshing & Rendering Pipeline (Unity/URP)

## Rendering Stack

| Component | Technology |
|-----------|-----------|
| Render pipeline | URP (Universal Render Pipeline) |
| Shaders | Hand-written HLSL, StructuredBuffer vertex fetch via SV_VertexID |
| Mesh storage | `MegaMeshBuffer` — persistent GPU GraphicsBuffers per render layer |
| Chunk rendering | GPU-driven indirect draw via `Graphics.RenderPrimitivesIndexedIndirect` |
| Culling | Compute shader frustum culling (`FrustumCull.compute`) + Hi-Z occlusion (`HiZPyramid`) |
| Vertex format | `PackedMeshVertex` (16 bytes, 4×uint32 bit-packed) |
| Textures | `Texture2DArray` (≤ 1024 layers) |

---

## Mesh Construction Pipeline

### Phase 1: Burst Job Produces MeshData

The `GreedyMeshJob` runs on a worker thread and writes vertices/indices into NativeContainers:

```csharp
[BurstCompile]
public struct GreedyMeshJob : IJob
{
    // Inputs (read-only)
    [ReadOnly] public NativeArray<StateId> ChunkData;
    [ReadOnly] public NativeArray<StateId> NeighborNorth;  // 32×32 border slice
    [ReadOnly] public NativeArray<StateId> NeighborSouth;
    [ReadOnly] public NativeArray<StateId> NeighborEast;
    [ReadOnly] public NativeArray<StateId> NeighborWest;
    [ReadOnly] public NativeArray<StateId> NeighborUp;
    [ReadOnly] public NativeArray<StateId> NeighborDown;
    [ReadOnly] public NativeStateRegistry StateRegistry;
    [ReadOnly] public NativeAtlasLookup AtlasLookup;

    // Outputs
    public NativeList<PackedMeshVertex> OpaqueVertices;
    public NativeList<int> OpaqueIndices;
    public NativeList<PackedMeshVertex> CutoutVertices;
    public NativeList<int> CutoutIndices;
    public NativeList<PackedMeshVertex> TranslucentVertices;
    public NativeList<int> TranslucentIndices;

    public void Execute()
    {
        // For each of 6 face directions:
        //   For each slice along that axis (32 slices):
        //     Build 32×32 face visibility mask
        //     Greedy merge identical adjacent faces
        //     Emit quads into appropriate vertex/index list
        //     Compute AO per vertex
    }
}
```

### Phase 2: GPU Buffer Upload

After a mesh job completes, `MeshScheduler.PollCompleted()` calls `ChunkMeshStore.UpdateRenderer()`:

```
ChunkMeshStore.UpdateRenderer(coord, opaqueVerts, opaqueIdx, cutoutVerts, cutoutIdx, transVerts, transIdx)
  → MegaMeshBuffer.AllocateOrUpdate(coord, vertices, indices)  [per layer]
     Three-path allocation:
     1. In-place reuse: chunk has slot, new data fits → overwrite
     2. Free-list first-fit: search coalescing free-list, split remainder
     3. Append at end: grow buffer by doubling if needed
  → MegaMeshBuffer.FlushDirtyToGpu()  [batched Lock/Unlock per buffer]
```

`MegaMeshBuffer` maintains a CPU-side mirror of vertex and index data. `WriteDataToMirror()` writes to the mirror and expands dirty ranges. `FlushDirtyToGpu()` uploads all dirty ranges in a single `LockBufferForWrite`/`UnlockBufferAfterWrite` pair per buffer, reducing GPU upload calls from per-chunk to per-layer-per-frame.

### Phase 3: GPU-Driven Indirect Draw

No GameObjects, MeshFilter, or MeshRenderer per chunk. `ChunkMeshStore` owns three `MegaMeshBuffer` instances (opaque, cutout, translucent) and draws via:

```
ChunkMeshStore.RenderAll(camera):
  1. FlushDirtyToGpu() on each layer (batched Lock/Unlock per buffer)
  2. FlushArgs() on each layer (update global indirect args)
  3. GPU culling (compute shader dispatch per layer):
     a. CSResetCull: restore instanceCount=1 for all slots with geometry
     b. CSFrustumCull: test AABB vs 6 frustum planes, set instanceCount=0 for culled
     c. CSOcclusionCull (optional): combined frustum + Hi-Z test, cull occluded chunks
  4. For each layer (opaque, cutout, translucent):
     Graphics.RenderPrimitivesIndexedIndirect(rp, Triangles, indexBuffer, perChunkArgsBuffer, ...)
```

Each chunk gets its own `DrawIndexedIndirectArgs` entry in the per-chunk args buffer. The compute shader sets `instanceCount=0` for culled chunks, producing zero GPU work. `ChunkMeshStore.RenderAll()` is called from `LateUpdate`.

---

## Greedy Meshing Algorithm

Algorithm is identical to the engine-agnostic version (binary greedy with uint32 row masks). Only the data types change for Burst:

```
For each face direction (6):
  For each slice (32):
    Build NativeArray<uint> rowMask (32 entries, one uint per row)
    Build NativeArray<ushort> rowBlockId (32 entries, for merge comparison)

    For each row y in 0..31:
      For each column x in 0..31:
        StateId current = ChunkData[index(x, y, sliceZ)]
        StateId neighbor = // block behind this face
        BlockStateCompact state = StateRegistry.GetState(current)
        BlockStateCompact neighborState = StateRegistry.GetState(neighbor)

        if (state.IsOpaque AND NOT neighborState.IsOpaque):
          rowMask[y] |= (1u << x)
          rowBlockId[y] = current.Value

    // Greedy merge phase
    For each row y:
      while rowMask[y] != 0:
        int startX = math.tzcnt(rowMask[y])  // first set bit
        ushort blockId = // block at startX
        int width = // count contiguous bits with same block
        int height = // extend downward while rows match

        // Emit quad
        EmitQuad(startX, y, width, height, face, blockId, ...)

        // Clear consumed bits from masks
```

### AO Calculation (Burst-Compatible)

```csharp
// Static method, called per-vertex during quad emission
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static byte ComputeAO(
    bool side1, bool side2, bool corner)
{
    if (side1 && side2) return 0; // fully occluded
    return (byte)(3 - ((side1 ? 1 : 0) + (side2 ? 1 : 0) + (corner ? 1 : 0)));
}
```

---

## Shader Architecture (URP HLSL)

Shaders read vertex data from `StructuredBuffer<PackedMeshVertex>` via `SV_VertexID`. The hardware index buffer (bound by `RenderPrimitivesIndexedIndirect`) remaps `SV_VertexID` before the fetch. No traditional vertex attributes (`POSITION`, `NORMAL`, `COLOR`) are used.

### Shared include: LithforgeVoxelCommon.hlsl

Provides:
- `FetchVertex(uint svVertexID)` — decodes a `PackedMeshVertex` from the `StructuredBuffer<GpuPackedVertex> _VertexBuffer` into a `DecodedVertex` struct with float3 position, normal, AO, light levels, texture indices, tint types, UV, and overlay info.
- `SampleBiomeTint(float3 worldPos, int tintType)` — reads the global `_BiomeParamMap` for temperature/humidity and samples `_GrassColormap`, `_FoliageColormap`, or `_WaterColorLUT` based on tint type.
- `ApplyOverlay(baseColor, tiledUV, worldPos, hasOverlay, overlayTexIndex, overlayTintType, atlas)` — alpha-blends an independently-tinted overlay texture onto the base color.
- `kLightGamma[16]` — precomputed Minecraft-style light gamma curve: `pow(0.8, 15 - level)`.
- `kNormals[6]` — cardinal direction lookup table (0=+X, 1=-X, 2=+Y, 3=-Y, 4=+Z, 5=-Z).
- `kLodScales[4]` — LOD voxel scale factors (1.0, 2.0, 4.0, 8.0).

### LithforgeVoxelOpaque.shader

Material properties: `_AOStrength`, `_SunLightFactor`, `_AmbientLight`.

4 passes:
1. **ForwardLit**: Vertex shader calls `FetchVertex()`, applies AO strength interpolation, gamma-corrected block/sun lighting, biome tinting, overlay blending, and directional Lambert shading with ambient floor.
2. **DepthOnly**: Minimal vertex fetch, outputs position only.
3. **ShadowCaster**: Applies shadow bias via URP's `ApplyShadowBias()`, handles punctual and directional lights.

```hlsl
// ForwardLit vertex shader (simplified)
Varyings vert(uint svVertexID : SV_VertexID)
{
    DecodedVertex dv = FetchVertex(svVertexID);

    Varyings output;
    VertexPositionInputs vertexInput = GetVertexPositionInputs(dv.positionOS);
    output.positionCS = vertexInput.positionCS;
    output.positionWS = vertexInput.positionWS;

    VertexNormalInputs normalInput = GetVertexNormalInputs(dv.normalOS);
    output.normalWS = normalInput.normalWS;

    output.uv = dv.uv;
    output.texIndex = dv.texIndex;
    output.baseTintType = dv.baseTintType;
    output.hasOverlay = dv.hasOverlay;
    output.overlayTexIndex = dv.overlayTexIndex;
    output.overlayTintType = dv.overlayTintType;

    output.ao = lerp(1.0h, dv.ao, (half)_AOStrength);
    half sunLight = dv.sunLight * (half)_SunLightFactor;
    output.light = max(dv.blockLight, sunLight);
    output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

    return output;
}
```

### Cutout and Translucent variants:

- **Cutout** (`LithforgeVoxelCutout.shader`): Same structure as opaque but with `clip(texColor.a - _AlphaClipThreshold)` in fragment, `"Queue"="AlphaTest"`, and `Cull Off` for double-sided rendering. DepthOnly and ShadowCaster passes also apply alpha clip.
- **Translucent**: Alpha blending enabled, `"Queue"="Transparent"`, back-to-front chunk sorting (per-chunk, not per-face).

---

## Vertex Format: PackedMeshVertex

**PackedMeshVertex** (16 bytes = 4 × uint32, `StructLayout.Sequential`):

```
Word0 (32 bits):
  bits  0-5:   posX        (6 bits, 0-63)
  bits  6-11:  posY        (6 bits, 0-63)
  bits 12-17:  posZ        (6 bits, 0-63)
  bits 18-20:  normal      (3 bits, 0-5 cardinal directions)
  bits 21-22:  ao          (2 bits, 0-3 → [0,1])
  bits 23-26:  blockLight  (4 bits, 0-15 → gamma-corrected)
  bits 27-30:  sunLight    (4 bits, 0-15 → gamma-corrected)
  bit  31:     fluidTop    (1 bit, applies -0.125 Y offset)

Word1 (32 bits):
  bits  0-9:   texIndex        (10 bits, 0-1023)
  bits 10-11:  baseTintType    (2 bits: 0=none, 1=grass, 2=foliage, 3=water)
  bit  12:     hasOverlay      (1 bit)
  bits 13-22:  overlayTexIndex (10 bits, 0-1023)
  bits 23-24:  overlayTintType (2 bits)
  bits 25-26:  lodScale        (2 bits: 0=×1, 1=×2, 2=×4, 3=×8)
  bits 27-31:  padding

Word2 (32 bits):
  bits  0-7:   uvX             (8 bits, greedy quad width in voxels)
  bits  8-15:  uvY             (8 bits, greedy quad height in voxels)
  bits 16-31:  chunkWorldX     (int16, coord.x × ChunkSize)

Word3 (32 bits):
  bits  0-15:  chunkWorldY     (int16, coord.y × ChunkSize)
  bits 16-31:  chunkWorldZ     (int16, coord.z × ChunkSize)
```

Shader-side: `FetchVertex(uint svVertexID)` in `LithforgeVoxelCommon.hlsl` decodes the packed vertex. The hardware index buffer remaps `SV_VertexID` before fetch. World position is reconstructed as `pos * lodScale + chunkWorldOffset`. The `SignExtend16()` helper converts unsigned 16-bit chunk coordinates to signed.

---

## Texture Strategy

### Texture2DArray

Each block face texture is a slice in a `Texture2DArray`. Advantages:
- No UV bleeding between tiles
- Clean point filtering at all distances
- Mipmapping works correctly per slice
- The texture array index is packed in `Word1` bits 0-9 (10 bits, max 1023 layers)

UV coordinates (`Word2` bits 0-15) store the greedy quad width/height in voxels. The fragment shader applies `frac()` to tile the texture across merged quads.

---

## LOD System

Dual-path meshing from `Generated` state, managed by separate schedulers:

```
Generated ─┬─ LODLevel == 0 → MeshScheduler (GreedyMeshJob)                           → Ready
            └─ LODLevel > 0 → LODScheduler  (VoxelDownsampleJob + LODGreedyMeshJob)   → Ready
```

### LOD Level Assignment

`LODScheduler.UpdateLODLevels()` runs every frame before mesh scheduling. It computes Chebyshev XZ distance (in chunk units) from the camera chunk and assigns:

| Distance | LOD Level | Downsample | Effective Grid |
|----------|-----------|-----------|----------------|
| < lod1Distance | 0 | None (full detail) | 32³ |
| >= lod1Distance | 1 | 2×2×2 merge | 16³ |
| >= lod2Distance | 2 | 4×4×4 merge | 8³ |
| >= lod3Distance | 3 | 8×8×8 merge | 4³ |

Distances are configured in `ChunkSettings` (defaults: lod1=4, lod2=8, lod3=14 chunks).

### VoxelDownsampleJob (Burst)

For each output cell, scans `Scale³` source voxels. If more than half are air, output is air. Otherwise picks the first opaque block found (majority-vote). No light data is accessed during downsampling.

### LODGreedyMeshJob (Burst)

Simplified greedy meshing for LOD chunks. Uses binary greedy merge (same algorithm as `GreedyMeshJob`) for large surface reduction, but with no AO, no neighbor borders, and no light data. Emits `PackedMeshVertex` with full brightness (AO=3, blockLight=15, sunLight=15). Single output list (opaque only). Variable grid size: 16 for LOD1, 8 for LOD2, 4 for LOD3.

The job chain is: `VoxelDownsampleJob.Schedule()` → `LODGreedyMeshJob.Schedule(downsampleHandle)`.

### Mesh Upload for LOD

LOD meshes use `ChunkMeshStore.UpdateRendererSingleMesh()` which writes to the opaque `MegaMeshBuffer` only and frees the cutout/translucent slots for that chunk.

### LOD Transitions

When a Ready chunk's LOD level changes:
- **LOD0 → LOD>0**: LODScheduler schedules a downsample+mesh job, replaces the mesh on completion.
- **LOD>0 → LOD0**: Chunk state resets to `Generated`, MeshScheduler picks it up for full greedy meshing.

Unity's built-in `LODGroup` component is NOT used because LOD decisions are chunk-distance-based, not per-object screen-size-based.

---

## GPU Culling Pipeline

All culling runs on the GPU via `FrustumCull.compute`. Three kernels (64 threads/group):

### ChunkBoundsGPU (32 bytes)

Per-chunk AABB stored as `float3 WorldMin` + pad + `float3 WorldMax` + pad. Laid out as 2×float4 for GPU cache alignment. One entry per chunk slot in a `StructuredBuffer`.

### CSResetCull

Restores `instanceCount=1` for all slots with `indexCount > 0`. Must run before the cull pass each frame because the previous frame's cull may have zeroed `instanceCount` for chunks that are now visible.

### CSFrustumCull

Tests each chunk AABB against 6 frustum planes using the positive-vertex method: for each plane, selects the AABB corner farthest along the plane normal. If that corner is behind the plane, the entire AABB is outside the frustum and `instanceCount` is set to 0.

### CSOcclusionCull

Combined frustum + Hi-Z occlusion test. Performs the frustum test first (cheap early-out), then:
1. Projects all 8 AABB corners to NDC space
2. Computes the screen-space bounding rectangle
3. Selects a Hi-Z mip level where the AABB covers approximately 2×2 texels
4. Samples the Hi-Z texture at 4 corners of the bounding rect, taking the MIN (farthest occluder in reversed-Z)
5. Compares the chunk's nearest depth against the Hi-Z depth — if the nearest point is farther, the chunk is fully occluded

If any AABB corner is behind the camera (straddles the near plane), the chunk is conservatively marked visible.

### HiZPyramid

Generates a hierarchical Z-buffer from the previous frame's `_CameraDepthTexture`. `RFloat` format, MIN of 2×2 blocks (reversed-Z conservative). Uses separate `RenderTexture` per mip level during generation to avoid DX11 read-write hazards, then copies all mips into a single combined mipmapped RT for compute shader sampling.

### CPU-Side Culling

- **MeshScheduler** uses CPU frustum info to **prioritize** in-frustum chunks for meshing. Off-frustum chunks still mesh if budget remains after in-frustum chunks.
- **LODScheduler** checks frustum intersection before scheduling LOD transitions for Ready chunks.
- **GenerationScheduler** consumes candidates from ChunkManager's forward-weighted loading queue (sorted by `dist² * (2 - dot)` in `UpdateLoadingQueue`).

### Distance Culling

Chunks beyond render distance are unloaded by `ChunkManager.UnloadDistantChunks()`. Dirty chunks are saved to `WorldStorage` before their NativeArrays return to the ChunkPool.

---

## Biome Tinting

`BiomeTintManager` manages a global GPU texture (`_BiomeParamMap`) for biome climate parameters, enabling shader-side colormap lookup.

### Global Biome Parameter Map

- Format: `RGBA32` — R=temperature, G=humidity, B=biomeId, A=reserved
- Toroidal wrapping: `Mod(worldBlockCoord, mapSize)` for addressing, `TextureWrapMode.Repeat`
- Point filtering (B channel stores discrete biomeId — bilinear would corrupt LUT lookups)
- Per-chunk uploads via staging texture + `Graphics.CopyTexture`
- `HashSet<int2>` tracks written chunk columns to avoid redundant uploads

### Shader-Side Lookup (in LithforgeVoxelCommon.hlsl)

`SampleBiomeTint(worldPos, tintType)`:
1. Sample temperature and humidity from `_BiomeParamMap` using toroidal UV
2. Altitude-adjusted temperature: `temp -= (y - seaLevel) / 600`
3. Minecraft-style colormap UV: `u = 1 - temp`, `v = 1 - (humidity × temp)`
4. For grass (tintType=1): sample `_GrassColormap`
5. For foliage (tintType=2): sample `_FoliageColormap`
6. For water (tintType=3): read biomeId from B channel, index into `_WaterColorLUT` (256×1 texture)

### Water Color LUT

256×1 texture indexed by biomeId. One pixel per biome, point-filtered. Default color: `(63, 118, 228)` blue. Built at startup from `BiomeDefinition` water colors.
