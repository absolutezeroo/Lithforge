// LithforgeVoxelCommon.hlsl
// Shared StructuredBuffer fetch logic for all Lithforge voxel shaders.
// Must be included AFTER Core.hlsl (and Lighting.hlsl if needed by the pass).
//
// MeshVertex C# layout (40 bytes, StructLayout.Sequential):
//   float3 Position  bytes  0-11
//   float3 Normal    bytes 12-23
//   half4  Color     bytes 24-31  (r=AO, g=blockLight, b=sunLight, a=encodedTexIndex)
//   float2 UV        bytes 32-39
//
// Color.a encoding: encodedTexIndex = texIndex + tintType * 1024
//   texIndex = encodedTexIndex & 0x3FF  (bits 0-9,  range 0-1023)
//   tintType = encodedTexIndex >> 10    (bits 10-11, range 0-3)
//
// HLSL StructuredBuffer<T> stride must match exactly. The 'half' type in HLSL
// struct members is silently promoted to float by the compiler, making stride 48
// instead of 40. Solution: pack Color as two uint words and unpack with f16tof32().

#ifndef LITHFORGE_VOXEL_COMMON_INCLUDED
#define LITHFORGE_VOXEL_COMMON_INCLUDED

// ---------------------------------------------------------------------------
// GPU-side vertex layout -- must produce stride == 40 bytes.
// The two uint words at bytes 24-31 hold the four float16 Color components:
//   colorWord0: bits 0-15 = Color.r (AO), bits 16-31 = Color.g (blockLight)
//   colorWord1: bits 0-15 = Color.b (sunLight), bits 16-31 = Color.a (encodedTexIndex)
// This matches the C# half4 memory layout on little-endian (x86/ARM).
// ---------------------------------------------------------------------------
struct GpuMeshVertex
{
    float3 position;   // 12 bytes (offset 0)
    float3 normal;     // 12 bytes (offset 12)
    uint   colorWord0; //  4 bytes (offset 24) -- packs Color.r and Color.g as float16
    uint   colorWord1; //  4 bytes (offset 28) -- packs Color.b and Color.a as float16
    float2 uv;         //  8 bytes (offset 32)
};
// Total: 40 bytes. Matches C# MeshVertex with StructLayout.Sequential.

// ---------------------------------------------------------------------------
// Buffer declaration. Bound per-draw from ChunkMeshStore via MaterialPropertyBlock.
// The index buffer is hardware-bound via RenderPrimitivesIndexedIndirect,
// so SV_VertexID gives the post-index vertex index directly.
// ---------------------------------------------------------------------------
StructuredBuffer<GpuMeshVertex> _VertexBuffer;

// ---------------------------------------------------------------------------
// Decoded working vertex -- plain floats, safe for URP transform helpers.
// ---------------------------------------------------------------------------
struct DecodedVertex
{
    float3 positionOS;
    float3 normalOS;
    half   ao;           // Color.r: 0=fully occluded, 1=unoccluded
    half   blockLight;   // Color.g: [0,1] normalised
    half   sunLight;     // Color.b: [0,1] normalised
    int    texIndex;     // Color.a: encoded texIndex + tintType*1024
    float2 uv;
};

// ---------------------------------------------------------------------------
// Fetch and decode one vertex from the StructuredBuffer.
// svVertexID comes from SV_VertexID. With RenderPrimitivesIndexedIndirect,
// the hardware index buffer has already remapped the ID to the actual vertex
// index, so we read directly from _VertexBuffer[svVertexID].
// ---------------------------------------------------------------------------
DecodedVertex FetchVertex(uint svVertexID)
{
    GpuMeshVertex raw = _VertexBuffer[svVertexID];

    DecodedVertex dv;
    dv.positionOS = raw.position;
    dv.normalOS   = raw.normal;
    dv.uv         = raw.uv;

    // Unpack float16 pairs using f16tof32().
    // f16tof32(x) extracts bits [0:15]; f16tof32(x >> 16) extracts bits [16:31].
    dv.ao         = (half)f16tof32(raw.colorWord0);
    dv.blockLight = (half)f16tof32(raw.colorWord0 >> 16u);
    dv.sunLight   = (half)f16tof32(raw.colorWord1);
    // texIndex is the encoded value (texIndex + tintType*1024), decoded later in vert().
    dv.texIndex   = (int)round(f16tof32(raw.colorWord1 >> 16u));

    return dv;
}

// ---------------------------------------------------------------------------
// Decode tintType from packed texIndex
// encodedTexIndex = realTexIndex + tintType * 1024
// ---------------------------------------------------------------------------
void DecodeTintedTexIndex(int encodedIndex, out int realTexIndex, out int tintType)
{
    tintType     = encodedIndex >> 10;    // bits 10-11
    realTexIndex = encodedIndex & 0x3FF;  // bits 0-9
}

// ---------------------------------------------------------------------------
// Global biome tinting resources (bound via Shader.SetGlobalTexture)
// ---------------------------------------------------------------------------
TEXTURE2D(_BiomeParamMap);
SAMPLER(sampler_BiomeParamMap);
TEXTURE2D(_GrassColormap);
SAMPLER(sampler_GrassColormap);
TEXTURE2D(_FoliageColormap);
SAMPLER(sampler_FoliageColormap);

float4 _BiomeMapTransform; // xy=unused, zw=1/mapSize

// ---------------------------------------------------------------------------
// Sample biome tint color at a world position
// tintType: 0=none, 1=grass, 2=foliage, 3=water
// ---------------------------------------------------------------------------
half3 SampleBiomeTint(float3 worldPos, int tintType)
{
    if (tintType == 0)
        return half3(1, 1, 1);

    // Sample temperature + humidity from global biome parameter map
    float2 biomeUV = worldPos.xz * _BiomeMapTransform.zw;
    float2 climate = SAMPLE_TEXTURE2D(_BiomeParamMap, sampler_BiomeParamMap, biomeUV).rg;

    float temp     = saturate(climate.r);
    float humidity = saturate(climate.g);

    // Minecraft colormap UV: x = temperature, y = humidity * temperature
    float2 colormapUV = float2(temp, humidity * temp);

    if (tintType == 1) // grass
    {
        return (half3)SAMPLE_TEXTURE2D(_GrassColormap, sampler_GrassColormap, colormapUV).rgb;
    }
    else if (tintType == 2) // foliage
    {
        return (half3)SAMPLE_TEXTURE2D(_FoliageColormap, sampler_FoliageColormap, colormapUV).rgb;
    }
    else // tintType == 3: water (per-biome, fallback to default blue)
    {
        return half3(0.247h, 0.463h, 0.894h);
    }
}

// ---------------------------------------------------------------------------
// Shared Varyings -- used by ForwardLit in all three shader variants.
// ---------------------------------------------------------------------------
struct Varyings
{
    float4 positionCS                    : SV_POSITION;
    float2 uv                           : TEXCOORD0;
    nointerpolation int texIndex        : TEXCOORD1;
    half   ao                           : TEXCOORD2;
    half   light                        : TEXCOORD3;
    float3 normalWS                     : TEXCOORD4;
    float  fogFactor                    : TEXCOORD5;
    nointerpolation int tintType        : TEXCOORD6;
    float3 positionWS                   : TEXCOORD7;
};

#endif // LITHFORGE_VOXEL_COMMON_INCLUDED
