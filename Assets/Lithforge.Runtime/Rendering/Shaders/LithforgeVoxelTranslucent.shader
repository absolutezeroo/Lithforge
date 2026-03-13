Shader "Lithforge/VoxelTranslucent"
{
    Properties
    {
        _AtlasArray ("Texture Atlas Array", 2DArray) = "" {}
        _AOStrength ("AO Strength", Range(0, 1)) = 0.4
        _SunLightFactor ("Sun Light Factor", Range(0, 1)) = 1.0
        _AmbientLight ("Ambient Light", Range(0, 1)) = 0.2
        _WaterAlpha ("Water Alpha", Range(0, 1)) = 0.6
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

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
                float _WaterAlpha;
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

                // Apply base tint
                half3 baseTint = SampleBiomeTint(input.positionWS, input.baseTintType);
                texColor.rgb *= baseTint;

                // Apply overlay
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

                return half4(finalColor, (half)_WaterAlpha);
            }
            ENDHLSL
        }
    }
}
