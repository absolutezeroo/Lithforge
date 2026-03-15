// LithforgePlayerModelCommon.hlsl
// Shared vertex fetch logic for the player model shaders.
// Must be included AFTER Core.hlsl.
//
// PlayerModelVertex C# layout (40 bytes, StructLayout.Sequential):
//   float3 Position  (12 bytes)
//   float3 Normal    (12 bytes)
//   float2 UV        (8 bytes)
//   uint   PartID    (4 bytes)
//   uint   Flags     (4 bytes)

#ifndef LITHFORGE_PLAYER_MODEL_COMMON
#define LITHFORGE_PLAYER_MODEL_COMMON

// GPU-side vertex layout — matches PlayerModelVertex C# struct exactly.
struct PlayerVertex
{
    float3 position;  // 12 bytes — local position relative to part pivot
    float3 normal;    // 12 bytes — local normal
    float2 uv;        // 8 bytes  — skin texture UV [0,1]
    uint   partID;    // 4 bytes  — bone index
    uint   flags;     // 4 bytes  — bit0: isOverlay
};

StructuredBuffer<PlayerVertex>  _PlayerVertexBuffer;
StructuredBuffer<float4x4>      _PartTransforms;    // 6 matrices (world-space)

TEXTURE2D(_SkinTex);
SAMPLER(sampler_SkinTex);

struct PlayerVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD0;
    float3 normalWS   : TEXCOORD1;
};

PlayerVaryings PlayerVert(uint svVertexID : SV_VertexID)
{
    PlayerVertex v = _PlayerVertexBuffer[svVertexID];

    float4x4 partMat = _PartTransforms[v.partID];
    float3 posWorld = mul(partMat, float4(v.position, 1.0)).xyz;
    float3 normalWorld = mul((float3x3)partMat, v.normal);

    PlayerVaryings o;
    o.positionCS = mul(UNITY_MATRIX_VP, float4(posWorld, 1.0));
    o.uv = v.uv;
    o.normalWS = normalize(normalWorld);
    return o;
}

// Shared first-person lighting: lambert (ndotl * 0.75 + 0.25) with sun factor and ambient.
// Requires Lighting.hlsl to be included before calling this function.
// Guarded so depth-only passes (which skip Lighting.hlsl) compile cleanly.
#ifdef UNIVERSAL_LIGHTING_INCLUDED
half3 ComputeFirstPersonLighting(half3 albedo, float3 normalWS, float sunLightFactor, float ambientLight)
{
    Light mainLight = GetMainLight();
    float ndotl = saturate(dot(normalWS, mainLight.direction));
    half lambert = (half)(ndotl * 0.75 + 0.25);

    half sunFactor = (half)sunLightFactor;
    half3 directColor = albedo * lambert * mainLight.color.rgb * sunFactor;
    half3 ambientColor = albedo * (half)ambientLight;
    return directColor + ambientColor;
}
#endif

#endif // LITHFORGE_PLAYER_MODEL_COMMON
