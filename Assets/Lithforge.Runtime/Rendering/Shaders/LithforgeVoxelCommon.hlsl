// LithforgeVoxelCommon.hlsl
// Shared StructuredBuffer fetch logic for all Lithforge voxel shaders.
// Must be included AFTER Core.hlsl (and Lighting.hlsl if needed by the pass).
//
// PackedMeshVertex C# layout (16 bytes, StructLayout.Sequential):
//   uint Word0  bytes 0-3
//   uint Word1  bytes 4-7
//   uint Word2  bytes 8-11
//   uint Word3  bytes 12-15
//
// word0: posX(6) | posY(6) | posZ(6) | normal(3) | ao(2) | blockLight(4) | sunLight(4) | fluidTop(1)
// word1: texIndex(10) | baseTintType(2) | hasOverlay(1) | overlayTexIndex(10) | overlayTintType(2) | lodScale(2) | pad(5)
// word2: uvX(8) | uvY(8) | chunkWorldX(16)
// word3: chunkWorldY(16) | chunkWorldZ(16)

#ifndef LITHFORGE_VOXEL_COMMON_INCLUDED
#define LITHFORGE_VOXEL_COMMON_INCLUDED

// ---------------------------------------------------------------------------
// GPU-side vertex layout -- 16 bytes. 4 packed uint32 words.
// ---------------------------------------------------------------------------
struct GpuPackedVertex
{
    uint word0;  // 4 bytes (offset 0)
    uint word1;  // 4 bytes (offset 4)
    uint word2;  // 4 bytes (offset 8)
    uint word3;  // 4 bytes (offset 12)
};
// Total: 16 bytes. Matches C# PackedMeshVertex with StructLayout.Sequential.

// ---------------------------------------------------------------------------
// Buffer declaration. Bound per-draw from ChunkMeshStore via MaterialPropertyBlock.
// The index buffer is hardware-bound via RenderPrimitivesIndexedIndirect,
// so SV_VertexID gives the post-index vertex index directly.
// ---------------------------------------------------------------------------
StructuredBuffer<GpuPackedVertex> _VertexBuffer;

// ---------------------------------------------------------------------------
// Normal lookup table (6 cardinal directions, indexed by 3-bit normal field).
// ---------------------------------------------------------------------------
static const float3 kNormals[6] =
{
    float3( 1, 0, 0),  // 0: +X
    float3(-1, 0, 0),  // 1: -X
    float3( 0, 1, 0),  // 2: +Y
    float3( 0,-1, 0),  // 3: -Y
    float3( 0, 0, 1),  // 4: +Z
    float3( 0, 0,-1),  // 5: -Z
};

// ---------------------------------------------------------------------------
// LOD voxel scale lookup (indexed by 2-bit lodScale field).
// ---------------------------------------------------------------------------
static const float kLodScales[4] = { 1.0, 2.0, 4.0, 8.0 };

// ---------------------------------------------------------------------------
// Minecraft-style light gamma: pow(0.8, 15-level).
// Precomputed for 0..15.
// ---------------------------------------------------------------------------
static const float kLightGamma[16] =
{
    0.035184, // 0: pow(0.8, 15)
    0.043980, // 1: pow(0.8, 14)
    0.054976, // 2: pow(0.8, 13)
    0.068719, // 3: pow(0.8, 12)
    0.085899, // 4: pow(0.8, 11)
    0.107374, // 5: pow(0.8, 10)
    0.134218, // 6: pow(0.8, 9)
    0.167772, // 7: pow(0.8, 8)
    0.209715, // 8: pow(0.8, 7)
    0.262144, // 9: pow(0.8, 6)
    0.327680, // 10: pow(0.8, 5)
    0.409600, // 11: pow(0.8, 4)
    0.512000, // 12: pow(0.8, 3)
    0.640000, // 13: pow(0.8, 2)
    0.800000, // 14: pow(0.8, 1)
    1.000000, // 15: pow(0.8, 0)
};

// ---------------------------------------------------------------------------
// Sign-extend a 16-bit uint to a signed int.
// HLSL has no int16 type; this XOR-subtract pattern converts unsigned bit
// pattern to two's complement signed.
// ---------------------------------------------------------------------------
int SignExtend16(uint v)
{
    return (int)((v ^ 0x8000u) - 0x8000u);
}

// ---------------------------------------------------------------------------
// Decoded working vertex -- plain floats, safe for URP transform helpers.
// ---------------------------------------------------------------------------
struct DecodedVertex
{
    float3 positionOS;
    float3 normalOS;
    half   ao;              // 0=fully occluded, 1=unoccluded (from 2-bit field 0-3)
    half   blockLight;      // [0,1] normalised (gamma-corrected from 4-bit level)
    half   sunLight;        // [0,1] normalised (gamma-corrected from 4-bit level)
    int    texIndex;        // 0-1023 base texture atlas index
    float2 uv;
    int    baseTintType;    // 0-3 (0=none, 1=grass, 2=foliage, 3=water)
    int    overlayTintType; // 0-3
    int    hasOverlay;      // 0 or 1
    int    overlayTexIndex; // 0-1023
    int    fluidTop;        // 1 if top face of fluid block, 0 otherwise
};

// ---------------------------------------------------------------------------
// Fetch and decode one vertex from the StructuredBuffer.
// svVertexID comes from SV_VertexID. With RenderPrimitivesIndexedIndirect,
// the hardware index buffer has already remapped the ID to the actual vertex
// index, so we read directly from _VertexBuffer[svVertexID].
// ---------------------------------------------------------------------------
DecodedVertex FetchVertex(uint svVertexID)
{
    GpuPackedVertex raw = _VertexBuffer[svVertexID];

    uint w0 = raw.word0;
    uint w1 = raw.word1;
    uint w2 = raw.word2;
    uint w3 = raw.word3;

    // Decode local grid position (6 bits each, 0-63)
    float posX = (float)(w0 & 0x3Fu);
    float posY = (float)((w0 >> 6u) & 0x3Fu);
    float posZ = (float)((w0 >> 12u) & 0x3Fu);

    // Normal direction (3 bits, 0-5)
    uint normalIdx = (w0 >> 18u) & 0x7u;

    // LOD voxel scale (2 bits from word1)
    float lodScale = kLodScales[(w1 >> 25u) & 0x3u];

    // Chunk world offset (int16 sign-extended)
    int cwx = SignExtend16(w2 >> 16u);
    int cwy = SignExtend16(w3 & 0xFFFFu);
    int cwz = SignExtend16(w3 >> 16u);

    // Reconstruct world-space position
    // pos * lodScale gives local position in world units, then add chunk world offset
    float3 worldPos = float3(
        posX * lodScale + (float)cwx,
        posY * lodScale + (float)cwy,
        posZ * lodScale + (float)cwz);

    // Fluid top face: bit 31 of word0. Apply -0.125 Y offset for water surface.
    uint fluidTop = (w0 >> 31u) & 0x1u;
    worldPos.y -= (float)fluidTop * 0.125;

    DecodedVertex dv;
    dv.positionOS = worldPos;
    dv.normalOS   = kNormals[normalIdx];

    // AO: 2-bit field (0-3), normalise to [0,1] where 3=unoccluded
    dv.ao = (half)((float)((w0 >> 21u) & 0x3u) / 3.0);

    // Light: 4-bit raw levels (0-15), apply Minecraft gamma curve
    uint blockLightLevel = (w0 >> 23u) & 0xFu;
    uint sunLightLevel   = (w0 >> 27u) & 0xFu;
    dv.blockLight = (half)kLightGamma[blockLightLevel];
    dv.sunLight   = (half)kLightGamma[sunLightLevel];

    // Texture index (10 bits)
    dv.texIndex = (int)(w1 & 0x3FFu);

    // Tint types
    dv.baseTintType    = (int)((w1 >> 10u) & 0x3u);
    dv.hasOverlay      = (int)((w1 >> 12u) & 0x1u);
    dv.overlayTexIndex = (int)((w1 >> 13u) & 0x3FFu);
    dv.overlayTintType = (int)((w1 >> 23u) & 0x3u);

    // Fluid top flag (already decoded above for Y offset)
    dv.fluidTop = (int)fluidTop;

    // UV: 8-bit each (greedy quad width/height in voxels), used for texture tiling
    dv.uv = float2((float)(w2 & 0xFFu), (float)((w2 >> 8u) & 0xFFu));

    return dv;
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
TEXTURE2D(_WaterColorLUT);
SAMPLER(sampler_WaterColorLUT);

float4 _BiomeMapScale; // xy=reserved, zw=1/mapSize (toroidal wrap via Repeat)
float _SeaLevel;       // world Y sea level for altitude-based tint adjustment

// ---------------------------------------------------------------------------
// Sample biome tint color at a world position
// tintType: 0=none, 1=grass, 2=foliage, 3=water
// ---------------------------------------------------------------------------
half3 SampleBiomeTint(float3 worldPos, int tintType)
{
    if (tintType == 0)
        return half3(1, 1, 1);

    // Sample temperature + humidity from global biome parameter map
    float2 biomeUV = worldPos.xz * _BiomeMapScale.zw;
    float2 climate = SAMPLE_TEXTURE2D(_BiomeParamMap, sampler_BiomeParamMap, biomeUV).rg;

    float temp     = saturate(climate.r);
    float rainfall = saturate(climate.g);

    // Minecraft altitude adjustment: temperature decreases above sea level
    // 0.00166667 = 1/600
    if (worldPos.y > _SeaLevel)
    {
        temp -= (worldPos.y - _SeaLevel) * 0.00166667;
        temp = saturate(temp);
    }

    // Minecraft colormap UV:
    //   adjustedRainfall = rainfall * temp (downfall is modulated by temperature)
    //   u = 1 - temp                      (hot=left, cold=right)
    //   v = 1 - adjustedRainfall          (humid=bottom, dry=top)
    float adjustedRainfall = rainfall * temp;
    float2 colormapUV = float2(1.0 - temp, 1.0 - adjustedRainfall);

    if (tintType == 1) // grass
    {
        return (half3)SAMPLE_TEXTURE2D(_GrassColormap, sampler_GrassColormap, colormapUV).rgb;
    }
    else if (tintType == 2) // foliage
    {
        return (half3)SAMPLE_TEXTURE2D(_FoliageColormap, sampler_FoliageColormap, colormapUV).rgb;
    }
    else // tintType == 3: water (per-biome lookup)
    {
        // Read biomeId from B channel of biome parameter map
        float biomeIdNorm = SAMPLE_TEXTURE2D(_BiomeParamMap, sampler_BiomeParamMap, biomeUV).b;
        float biomeId = biomeIdNorm * 255.0;
        // Sample water color LUT (256x1 texture, Point filtering)
        float2 lutUV = float2((biomeId + 0.5) / 256.0, 0.5);
        return (half3)SAMPLE_TEXTURE2D_LOD(_WaterColorLUT, sampler_WaterColorLUT, lutUV, 0).rgb;
    }
}

// ---------------------------------------------------------------------------
// Shared Varyings -- used by ForwardLit in all three shader variants.
// ---------------------------------------------------------------------------
struct Varyings
{
    float4 positionCS                              : SV_POSITION;
    float2 uv                                      : TEXCOORD0;
    nointerpolation int texIndex                   : TEXCOORD1;
    half   ao                                      : TEXCOORD2;
    half   light                                   : TEXCOORD3;
    float3 normalWS                                : TEXCOORD4;
    float  fogFactor                               : TEXCOORD5;
    nointerpolation int baseTintType               : TEXCOORD6;
    float3 positionWS                              : TEXCOORD7;
    nointerpolation int hasOverlay                 : TEXCOORD8;
    nointerpolation int overlayTexIndex            : TEXCOORD9;
    nointerpolation int overlayTintType            : TEXCOORD10;
    nointerpolation int isWaterTop                 : TEXCOORD11;
};

// ---------------------------------------------------------------------------
// Apply overlay texture (alpha-blended, independently tinted) on top of base color.
// _AtlasArray and sampler must be passed as parameters since they are declared
// per-shader (not global).
// ---------------------------------------------------------------------------
half3 ApplyOverlay(
    half3 baseColor,
    float2 tiledUV,
    float3 worldPos,
    int hasOverlay,
    int overlayTexIndex,
    int overlayTintType,
    TEXTURE2D_ARRAY_PARAM(atlasArray, atlasSampler))
{
    if (hasOverlay == 0)
        return baseColor;

    half4 overlayColor = SAMPLE_TEXTURE2D_ARRAY(atlasArray, atlasSampler, tiledUV, overlayTexIndex);

    // Tint the overlay
    half3 overlayTint = SampleBiomeTint(worldPos, overlayTintType);
    half3 tintedOverlay = overlayColor.rgb * overlayTint;

    // Alpha blend overlay onto base
    return baseColor * (1.0h - overlayColor.a) + tintedOverlay * overlayColor.a;
}

#endif // LITHFORGE_VOXEL_COMMON_INCLUDED
