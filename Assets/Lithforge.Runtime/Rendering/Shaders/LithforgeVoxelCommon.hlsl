// LithforgeVoxelCommon.hlsl
// Shared StructuredBuffer fetch logic for all Lithforge voxel shaders.
// Must be included AFTER Core.hlsl (and Lighting.hlsl if needed by the pass).
//
// MeshVertex C# layout (48 bytes, StructLayout.Sequential):
//   float3 Position     bytes  0-11
//   float3 Normal       bytes 12-23
//   half4  Color        bytes 24-31  (r=AO, g=blockLight, b=sunLight, a=baseTexIndex)
//   float2 UV           bytes 32-39
//   uint   TintOverlay  bytes 40-43  (packed per-face tint + overlay info)
//   uint   Pad          bytes 44-47  (16-byte alignment)
//
// Color.a encoding: pure baseTexIndex (no tintType encoding)
//
// TintOverlay packing (uint, 32 bits):
//   bits  0-1  : baseTintType     (0=none, 1=grass, 2=foliage, 3=water)
//   bits  2-3  : overlayTintType  (0=none, 1=grass, 2=foliage, 3=water)
//   bit   4    : hasOverlay       (0=no overlay, 1=has overlay)
//   bits  5-14 : overlayTexIndex  (0-1023, index into atlas array)
//   bits 15-31 : reserved (0)
//
// HLSL StructuredBuffer<T> stride must match exactly. The 'half' type in HLSL
// struct members is silently promoted to float by the compiler, making stride wrong.
// Solution: pack Color as two uint words and unpack with f16tof32().

#ifndef LITHFORGE_VOXEL_COMMON_INCLUDED
#define LITHFORGE_VOXEL_COMMON_INCLUDED

// ---------------------------------------------------------------------------
// GPU-side vertex layout -- must produce stride == 48 bytes.
// The two uint words at bytes 24-31 hold the four float16 Color components:
//   colorWord0: bits 0-15 = Color.r (AO), bits 16-31 = Color.g (blockLight)
//   colorWord1: bits 0-15 = Color.b (sunLight), bits 16-31 = Color.a (baseTexIndex)
// This matches the C# half4 memory layout on little-endian (x86/ARM).
// ---------------------------------------------------------------------------
struct GpuMeshVertex
{
    float3 position;    // 12 bytes (offset 0)
    float3 normal;      // 12 bytes (offset 12)
    uint   colorWord0;  //  4 bytes (offset 24) -- packs Color.r and Color.g as float16
    uint   colorWord1;  //  4 bytes (offset 28) -- packs Color.b and Color.a as float16
    float2 uv;          //  8 bytes (offset 32)
    uint   tintOverlay; //  4 bytes (offset 40) -- packed per-face tint + overlay
    uint   _pad;        //  4 bytes (offset 44) -- 16-byte alignment
};
// Total: 48 bytes. Matches C# MeshVertex with StructLayout.Sequential.

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
    half   ao;              // Color.r: 0=fully occluded, 1=unoccluded
    half   blockLight;      // Color.g: [0,1] normalised
    half   sunLight;        // Color.b: [0,1] normalised
    int    texIndex;        // Color.a: pure base texIndex (no tint encoding)
    float2 uv;
    int    baseTintType;    // bits 0-1 of TintOverlay (0-3)
    int    overlayTintType; // bits 2-3 of TintOverlay (0-3)
    int    hasOverlay;      // bit 4 of TintOverlay (0 or 1)
    int    overlayTexIndex; // bits 5-14 of TintOverlay (0-1023)
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
    // texIndex is now a pure base index (no tintType encoding)
    dv.texIndex   = (int)round(f16tof32(raw.colorWord1 >> 16u));

    // Decode TintOverlay
    uint to = raw.tintOverlay;
    dv.baseTintType    = (int)(to & 0x3);
    dv.overlayTintType = (int)((to >> 2) & 0x3);
    dv.hasOverlay      = (int)((to >> 4) & 0x1);
    dv.overlayTexIndex = (int)((to >> 5) & 0x3FF);

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
