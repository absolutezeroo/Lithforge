Shader "Lithforge/HeldItem"
{
    Properties
    {
        _AtlasArray ("Texture Atlas Array", 2DArray) = "" {}
        _SunLightFactor ("Sun Light Factor", Range(0, 1)) = 1.0
        _AmbientLight ("Ambient Light", Range(0, 1)) = 0.2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry+12"
        }

        Pass
        {
            Name "HeldItemForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest Always

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex HeldItemVert
            #pragma fragment HeldItemFrag
            #pragma require 2darray

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "LithforgePlayerModelCommon.hlsl"

            struct HeldItemVertexData
            {
                float3 position;  // 12 bytes
                float3 normal;    // 12 bytes
                float2 uv;        // 8 bytes
                uint   texIndex;  // 4 bytes
                uint   padding;   // 4 bytes
            };

            StructuredBuffer<HeldItemVertexData> _HeldItemVertexBuffer;

            TEXTURE2D_ARRAY(_AtlasArray);
            SAMPLER(sampler_AtlasArray);

            CBUFFER_START(UnityPerMaterial)
                float _SunLightFactor;
                float _AmbientLight;
            CBUFFER_END

            struct HeldItemVaryings
            {
                float4 positionCS                    : SV_POSITION;
                float2 uv                            : TEXCOORD0;
                float3 normalWS                      : TEXCOORD1;
                nointerpolation uint texIndex        : TEXCOORD2;
            };

            HeldItemVaryings HeldItemVert(uint svVertexID : SV_VertexID)
            {
                HeldItemVertexData v = _HeldItemVertexBuffer[svVertexID];

                // Transform by main arm part matrix (index 3 = left arm at +X = right side of screen)
                float4x4 partMat = _PartTransforms[3];
                float3 posWorld = mul(partMat, float4(v.position, 1.0)).xyz;
                float3 normalWorld = mul((float3x3)partMat, v.normal);

                HeldItemVaryings o;
                o.positionCS = mul(UNITY_MATRIX_VP, float4(posWorld, 1.0));
                o.uv = v.uv;
                o.normalWS = normalize(normalWorld);
                o.texIndex = v.texIndex;
                return o;
            }

            half4 HeldItemFrag(HeldItemVaryings i) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D_ARRAY(
                    _AtlasArray, sampler_AtlasArray, i.uv, i.texIndex);

                half3 finalColor = ComputeFirstPersonLighting(
                    texColor.rgb, i.normalWS, _SunLightFactor, _AmbientLight);

                return half4(finalColor, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "HeldItemDepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex HeldItemDepthVert
            #pragma fragment HeldItemDepthFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct HeldItemVertexData
            {
                float3 position;
                float3 normal;
                float2 uv;
                uint   texIndex;
                uint   padding;
            };

            StructuredBuffer<HeldItemVertexData> _HeldItemVertexBuffer;

            // _PartTransforms declared in PlayerModelCommon.hlsl
            // but depth-only pass does not include it, so redeclare here
            StructuredBuffer<float4x4>           _PartTransforms;

            struct HeldItemDepthVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            HeldItemDepthVaryings HeldItemDepthVert(uint svVertexID : SV_VertexID)
            {
                HeldItemVertexData v = _HeldItemVertexBuffer[svVertexID];
                float4x4 partMat = _PartTransforms[3];
                float3 posWorld = mul(partMat, float4(v.position, 1.0)).xyz;

                HeldItemDepthVaryings o;
                o.positionCS = mul(UNITY_MATRIX_VP, float4(posWorld, 1.0));
                return o;
            }

            half4 HeldItemDepthFrag(HeldItemDepthVaryings i) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
