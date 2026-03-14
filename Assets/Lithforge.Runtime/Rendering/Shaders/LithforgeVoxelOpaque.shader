Shader "Lithforge/VoxelOpaque"
{
    Properties
    {
        _AtlasArray ("Texture Atlas Array", 2DArray) = "" {}
        _AOStrength ("AO Strength", Range(0, 1)) = 0.4
        _SunLightFactor ("Sun Light Factor", Range(0, 1)) = 1.0
        _AmbientLight ("Ambient Light", Range(0, 1)) = 0.2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
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
            CBUFFER_END

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

                // Per-face tint + overlay
                output.baseTintType = dv.baseTintType;
                output.hasOverlay = dv.hasOverlay;
                output.overlayTexIndex = dv.overlayTexIndex;
                output.overlayTintType = dv.overlayTintType;

                // AO: color.r is ao/3 (0=fully occluded, 1=unoccluded)
                output.ao = lerp(1.0h, dv.ao, (half)_AOStrength);

                // Lighting: block light and sun light
                half sunLight = dv.sunLight * (half)_SunLightFactor;
                output.light = max(dv.blockLight, sunLight);

                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Tile UV for greedy quads (frac wraps the texture)
                float2 tiledUV = frac(input.uv);

                half4 texColor = SAMPLE_TEXTURE2D_ARRAY(
                    _AtlasArray, sampler_AtlasArray, tiledUV, input.texIndex);

                // 1. Apply base tint (full-face, e.g. grass_block top, oak_leaves)
                half3 baseTint = SampleBiomeTint(input.positionWS, input.baseTintType);
                texColor.rgb *= baseTint;

                // 2. Apply overlay (e.g. grass_block sides: overlay with its own tint)
                texColor.rgb = ApplyOverlay(
                    texColor.rgb, tiledUV, input.positionWS,
                    input.hasOverlay, input.overlayTexIndex, input.overlayTintType,
                    TEXTURE2D_ARRAY_ARGS(_AtlasArray, sampler_AtlasArray));

                // Directional lighting with ambient
                Light mainLight = GetMainLight();
                float ndotl = saturate(dot(input.normalWS, mainLight.direction));
                half lambert = (half)(ndotl * 0.6 + 0.4);

                // Voxel light modulates direct lighting; ambient is a floor independent of light level
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
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex DepthOnlyVert
            #pragma fragment DepthOnlyFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "LithforgeVoxelCommon.hlsl"

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            DepthVaryings DepthOnlyVert(uint svVertexID : SV_VertexID)
            {
                DecodedVertex dv = FetchVertex(svVertexID);

                DepthVaryings output;
                output.positionCS = TransformObjectToHClip(dv.positionOS);
                return output;
            }

            half4 DepthOnlyFrag(DepthVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
