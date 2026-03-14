Shader "Lithforge/VoxelCutout"
{
    Properties
    {
        _AtlasArray ("Texture Atlas Array", 2DArray) = "" {}
        _AOStrength ("AO Strength", Range(0, 1)) = 0.4
        _SunLightFactor ("Sun Light Factor", Range(0, 1)) = 1.0
        _AmbientLight ("Ambient Light", Range(0, 1)) = 0.2
        _AlphaClipThreshold ("Alpha Clip Threshold", Range(0.01, 1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma require 2darray

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "LithforgeVoxelCommon.hlsl"

            TEXTURE2D_ARRAY(_AtlasArray);
            SAMPLER(sampler_AtlasArray);

            CBUFFER_START(UnityPerMaterial)
                float _AOStrength;
                float _SunLightFactor;
                float _AmbientLight;
                float _AlphaClipThreshold;
            CBUFFER_END

            Varyings vert(uint svVertexID : SV_VertexID)
            {
                DecodedVertex dv = FetchVertex(svVertexID);

                Varyings output;

                VertexPositionInputs posInputs = GetVertexPositionInputs(dv.positionOS);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(dv.normalOS);
                output.uv = dv.uv;
                output.texIndex = dv.texIndex;

                // Per-face tint + overlay
                output.baseTintType = dv.baseTintType;
                output.hasOverlay = dv.hasOverlay;
                output.overlayTexIndex = dv.overlayTexIndex;
                output.overlayTintType = dv.overlayTintType;

                output.ao = lerp(1.0h, dv.ao, (half)_AOStrength);

                half sunLight = dv.sunLight * (half)_SunLightFactor;
                output.light = max(dv.blockLight, sunLight);

                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 tiledUV = frac(input.uv);
                half4 texColor = SAMPLE_TEXTURE2D_ARRAY(
                    _AtlasArray, sampler_AtlasArray, tiledUV, input.texIndex);

                // Clip on base texture alpha (before tinting — overlay doesn't change silhouette)
                clip(texColor.a - _AlphaClipThreshold);

                // Apply base tint
                half3 baseTint = SampleBiomeTint(input.positionWS, input.baseTintType);
                texColor.rgb *= baseTint;

                // Apply overlay
                texColor.rgb = ApplyOverlay(
                    texColor.rgb, tiledUV, input.positionWS,
                    input.hasOverlay, input.overlayTexIndex, input.overlayTintType,
                    TEXTURE2D_ARRAY_ARGS(_AtlasArray, sampler_AtlasArray));

                Light mainLight = GetMainLight();
                float ndotl = saturate(dot(input.normalWS, mainLight.direction));
                half lambert = (half)(ndotl * 0.6 + 0.4);

                half3 directColor = texColor.rgb * lambert * mainLight.color.rgb * input.light;
                half3 ambientColor = texColor.rgb * (half)_AmbientLight;
                half3 finalColor = (directColor + ambientColor) * input.ao;
                finalColor = MixFog(finalColor, input.fogFactor);

                return half4(finalColor, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex DepthOnlyVert
            #pragma fragment DepthOnlyFrag
            #pragma require 2darray

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "LithforgeVoxelCommon.hlsl"

            TEXTURE2D_ARRAY(_AtlasArray);
            SAMPLER(sampler_AtlasArray);

            CBUFFER_START(UnityPerMaterial)
                float _AlphaClipThreshold;
            CBUFFER_END

            struct CutoutDepthVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                nointerpolation int texIndex : TEXCOORD1;
            };

            CutoutDepthVaryings DepthOnlyVert(uint svVertexID : SV_VertexID)
            {
                DecodedVertex dv = FetchVertex(svVertexID);

                // texIndex is already pure (no tintType encoding to strip)
                CutoutDepthVaryings output;
                output.positionCS = TransformObjectToHClip(dv.positionOS);
                output.uv = dv.uv;
                output.texIndex = dv.texIndex;
                return output;
            }

            half4 DepthOnlyFrag(CutoutDepthVaryings input) : SV_Target
            {
                float2 tiledUV = frac(input.uv);
                half4 texColor = SAMPLE_TEXTURE2D_ARRAY(
                    _AtlasArray, sampler_AtlasArray, tiledUV, input.texIndex);
                clip(texColor.a - _AlphaClipThreshold);
                return 0;
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma require 2darray

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "LithforgeVoxelCommon.hlsl"

            TEXTURE2D_ARRAY(_AtlasArray);
            SAMPLER(sampler_AtlasArray);

            CBUFFER_START(UnityPerMaterial)
                float _AlphaClipThreshold;
            CBUFFER_END

            struct CutoutShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                nointerpolation int texIndex : TEXCOORD1;
            };

            float3 _LightDirection;
            float3 _LightPosition;

            CutoutShadowVaryings ShadowVert(uint svVertexID : SV_VertexID)
            {
                DecodedVertex dv = FetchVertex(svVertexID);

                float3 positionWS = TransformObjectToWorld(dv.positionOS);
                float3 normalWS = TransformObjectToWorldNormal(dv.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 posCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

                #if UNITY_REVERSED_Z
                    posCS.z = min(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    posCS.z = max(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                CutoutShadowVaryings output;
                output.positionCS = posCS;
                output.uv = dv.uv;
                output.texIndex = dv.texIndex;
                return output;
            }

            half4 ShadowFrag(CutoutShadowVaryings input) : SV_Target
            {
                float2 tiledUV = frac(input.uv);
                half4 texColor = SAMPLE_TEXTURE2D_ARRAY(
                    _AtlasArray, sampler_AtlasArray, tiledUV, input.texIndex);
                clip(texColor.a - _AlphaClipThreshold);
                return 0;
            }
            ENDHLSL
        }
    }
}
