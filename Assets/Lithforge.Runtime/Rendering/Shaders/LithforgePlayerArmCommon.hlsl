// LithforgePlayerArmCommon.hlsl
// Shared vertex fetch logic for the first-person arm shaders.
// Must be included AFTER Core.hlsl.
//
// PlayerArmVertex C# layout (40 bytes, StructLayout.Sequential):
//   float3 Position  (12 bytes)
//   float3 Normal    (12 bytes)
//   float2 UV        (8 bytes)
//   uint   PartID    (4 bytes)
//   uint   Flags     (4 bytes)

#ifndef LITHFORGE_PLAYER_ARM_COMMON
#define LITHFORGE_PLAYER_ARM_COMMON

// GPU-side vertex layout — matches PlayerArmVertex C# struct exactly.
struct ArmVertex
{
    float3 position;  // 12 bytes — local position relative to part pivot
    float3 normal;    // 12 bytes — local normal
    float2 uv;        // 8 bytes  — skin texture UV [0,1]
    uint   partID;    // 4 bytes  — bone index
    uint   flags;     // 4 bytes  — bit0: isOverlay
};

StructuredBuffer<ArmVertex>  _ArmVertexBuffer;
StructuredBuffer<float4x4>   _PartTransforms;    // 6 matrices
float4x4                     _ArmToClip;          // custom VP with FOV=70, near=0.05

TEXTURE2D(_SkinTex);
SAMPLER(sampler_SkinTex);

struct ArmVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD0;
    float3 normalWS   : TEXCOORD1;
};

ArmVaryings ArmVert(uint svVertexID : SV_VertexID)
{
    ArmVertex v = _ArmVertexBuffer[svVertexID];

    float4x4 partMat = _PartTransforms[v.partID];
    float3 posModel = mul(partMat, float4(v.position, 1.0)).xyz;
    float3 normalModel = mul((float3x3)partMat, v.normal);

    ArmVaryings o;
    o.positionCS = mul(_ArmToClip, float4(posModel, 1.0));
    o.uv = v.uv;
    o.normalWS = normalize(normalModel);
    return o;
}

#endif // LITHFORGE_PLAYER_ARM_COMMON
