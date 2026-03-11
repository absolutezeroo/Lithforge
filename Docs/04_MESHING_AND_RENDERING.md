# Lithforge — Meshing & Rendering Pipeline (Unity/URP)

## Rendering Stack

| Layer | Technology | Purpose |
|-------|-----------|---------|
| Render pipeline | Universal Render Pipeline (URP) | Optimized forward renderer, SRP Batcher |
| Shaders | Hand-written HLSL (URP-compatible) | Atlas sampling, AO, voxel lighting |
| Mesh construction | `Mesh.MeshDataArray` (Burst-writable) | Zero-copy mesh building in jobs |
| Chunk rendering | One `MeshRenderer` per chunk per pass | Simple, GPU-instancing compatible |
| Texture format | `Texture2DArray` | Per-face textures without atlas UV bleeding |

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
    public NativeList<MeshVertex> OpaqueVertices;
    public NativeList<int> OpaqueIndices;
    public NativeList<MeshVertex> CutoutVertices;
    public NativeList<int> CutoutIndices;
    public NativeList<MeshVertex> TranslucentVertices;
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

### Phase 2: Main Thread Uploads to GPU

```csharp
// MeshUploader.cs — called on main thread after job completes
public static class MeshUploader
{
    public static void UploadChunkMesh(
        Mesh targetMesh,
        NativeList<MeshVertex> vertices,
        NativeList<int> indices)
    {
        // Method A: Mesh.MeshDataArray (zero-copy, recommended)
        Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(1);
        Mesh.MeshData md = meshDataArray[0];

        md.SetVertexBufferParams(vertices.Length, MeshVertex.VertexAttributes);
        md.SetIndexBufferParams(indices.Length, IndexFormat.UInt32);

        // Copy from NativeList → MeshData (both are native, fast memcpy)
        NativeArray<MeshVertex> verts = md.GetVertexData<MeshVertex>();
        verts.CopyFrom(vertices.AsArray());

        NativeArray<int> idx = md.GetIndexData<int>();
        idx.CopyFrom(indices.AsArray());

        md.subMeshCount = 1;
        md.SetSubMesh(0, new SubMeshDescriptor(0, indices.Length));

        Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, targetMesh);
        targetMesh.RecalculateBounds();
    }
}
```

### Phase 3: ChunkRenderer Displays Mesh

```csharp
// ChunkRenderer.cs — MonoBehaviour on each chunk's GameObject
public sealed class ChunkRenderer : MonoBehaviour
{
    [SerializeField] private MeshFilter _meshFilter;
    [SerializeField] private MeshRenderer _meshRenderer;

    private Mesh _opaqueMesh;
    private Mesh _cutoutMesh;
    private Mesh _translucentMesh;

    public void Initialize(int3 chunkWorldPosition, Material opaqueMat, Material cutoutMat, Material translucentMat)
    {
        transform.position = new Vector3(
            chunkWorldPosition.x * ChunkConstants.Size,
            chunkWorldPosition.y * ChunkConstants.Size,
            chunkWorldPosition.z * ChunkConstants.Size);

        _opaqueMesh = new Mesh { name = $"Chunk_{chunkWorldPosition}" };
        _meshFilter.sharedMesh = _opaqueMesh;
        _meshRenderer.sharedMaterial = opaqueMat;
        // Cutout and translucent as additional submeshes or child GameObjects
    }

    public void UpdateMesh(NativeList<MeshVertex> vertices, NativeList<int> indices)
    {
        MeshUploader.UploadChunkMesh(_opaqueMesh, vertices, indices);
    }
}
```

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

### LithforgeVoxelOpaque.shader

```hlsl
Shader "Lithforge/VoxelOpaque"
{
    Properties
    {
        _AtlasArray ("Texture Atlas Array", 2DArray) = "" {}
        _AOIntensity ("AO Intensity", Range(0, 1)) = 0.5
        _SunLightFactor ("Sun Light Factor", Range(0, 1)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_ARRAY(_AtlasArray);
            SAMPLER(sampler_AtlasArray); // point filtering for pixel art
            float _AOIntensity;
            float _SunLightFactor;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;  // r=AO, g=blockLight, b=sunLight, a=textureIndex
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 uv : TEXCOORD0;  // xy=UV, z=array index
                half ao : TEXCOORD1;
                half light : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = float3(input.uv, input.color.a * 255.0); // texture array index
                output.ao = 1.0 - (input.color.r * _AOIntensity);
                float blockLight = input.color.g;
                float sunLight = input.color.b * _SunLightFactor;
                output.light = max(blockLight, sunLight);
                output.light = max(output.light, 0.05); // ambient minimum
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D_ARRAY(
                    _AtlasArray, sampler_AtlasArray, input.uv.xy, (int)input.uv.z);

                half3 finalColor = texColor.rgb * input.ao * input.light;
                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
}
```

### Cutout and Translucent variants:

- **Cutout**: Same as opaque but with `clip(texColor.a - 0.5)` in fragment, `"Queue"="AlphaTest"`, and `Cull Off` for double-sided rendering.
- **Translucent**: Alpha blending enabled, `"Queue"="Transparent"`, back-to-front chunk sorting (per-chunk, not per-face).

---

## Texture Strategy

### Texture2DArray (Recommended)

Each block face texture is a slice in a `Texture2DArray`. Advantages:
- No UV bleeding between tiles
- Clean point filtering at all distances
- Mipmapping works correctly per slice
- The texture array index is passed per-vertex (in `color.a`)

```csharp
// TextureAtlasManager.cs — builds Texture2DArray from content PNGs
public Texture2DArray BuildTextureArray(Dictionary<ResourceId, byte[]> texturePngs, int tileSize)
{
    int count = texturePngs.Count;
    Texture2DArray array = new Texture2DArray(
        tileSize, tileSize, count,
        TextureFormat.RGBA32, true /* mipmaps */, false /* linear */);

    array.filterMode = FilterMode.Point;
    array.wrapMode = TextureWrapMode.Repeat;

    int index = 0;
    foreach (KeyValuePair<ResourceId, byte[]> kvp in texturePngs)
    {
        Texture2D temp = new Texture2D(tileSize, tileSize, TextureFormat.RGBA32, false);
        temp.LoadImage(kvp.Value);
        array.SetPixelData(temp.GetRawTextureData<byte>(), 0, index);
        Object.Destroy(temp);
        index++;
    }

    array.Apply(true /* update mipmaps */, true /* make non-readable */);
    return array;
}
```

---

## LOD System

Dual-path meshing from `Generated` state, managed by separate schedulers:

```
Generated ─┬─ LODLevel == 0 → MeshScheduler (GreedyMeshJob)                    → Ready
            └─ LODLevel > 0 → LODScheduler  (VoxelDownsampleJob + LODMeshJob)   → Ready
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

### LODMeshJob (Burst)

Iterates the downsampled grid and emits simple culled-face quads (no greedy merging, no AO). Vertices are baked with full brightness: `Color = half4(1, 1, 1, texIndex)` (AO=1, blockLight=1, sunLight=1). Single submesh (opaque only). Boundary faces at grid edges are always emitted.

The job chain is: `VoxelDownsampleJob.Schedule()` → `LODMeshJob.Schedule(downsampleHandle)`.

### Mesh Upload for LOD

LOD meshes use `ChunkRenderManager.UpdateRendererSingleMesh()` which calls `MeshUploader.Upload()` with a single submesh (vs. 3 submeshes for full-detail). The mesh is completely replaced — no `MeshFilter.sharedMesh` swap.

### LOD Transitions

When a Ready chunk's LOD level changes:
- **LOD0 → LOD>0**: LODScheduler schedules a downsample+mesh job, replaces the mesh on completion.
- **LOD>0 → LOD0**: Chunk state resets to `Generated`, MeshScheduler picks it up for full greedy meshing.

Unity's built-in `LODGroup` component is NOT used because LOD decisions are chunk-distance-based, not per-object screen-size-based.

---

## Culling

### Frustum Culling

`ChunkCulling` extracts frustum planes via `GeometryUtility.CalculateFrustumPlanes()` and tests chunk AABBs with `TestPlanesAABB()`.

- **MeshScheduler** uses frustum info to **prioritize** in-frustum chunks for meshing. Off-frustum chunks still mesh if budget remains after in-frustum chunks.
- **LODScheduler** checks frustum intersection before scheduling LOD transitions for Ready chunks.
- **GenerationScheduler** sorts generation candidates with frustum-visible chunks first (stable partial sort).

### Distance Culling

Chunks beyond render distance are unloaded by `ChunkManager.UnloadDistantChunks()`. Dirty chunks are saved to `WorldStorage` before their NativeArrays return to the ChunkPool.
