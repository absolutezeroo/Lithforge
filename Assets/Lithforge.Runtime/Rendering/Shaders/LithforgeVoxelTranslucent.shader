Shader "Lithforge/VoxelTranslucent"
{
    Properties
    {
        _AtlasArray ("Texture Atlas Array", 2DArray) = "" {}
        _AOStrength ("AO Strength", Range(0, 1)) = 0.4
        _SunLightFactor ("Sun Light Factor", Range(0, 1)) = 1.0
        _AmbientLight ("Ambient Light", Range(0, 1)) = 0.2
        _WaterAlpha ("Water Alpha", Range(0, 1)) = 0.6

        [Header(Water)]
        _WaveAmplitude ("Wave Amplitude", Float) = 0.04
        _WaveFrequency ("Wave Frequency", Float) = 2.5
        _WaveSpeed ("Wave Speed", Float) = 1.2
        _FresnelPower ("Fresnel Power", Float) = 3.0
        _FresnelMin ("Fresnel Min Alpha", Range(0, 1)) = 0.3
        _FresnelMax ("Fresnel Max Alpha", Range(0, 1)) = 0.85
        _SpecularPower ("Specular Power", Float) = 64.0
        _SpecularIntensity ("Specular Intensity", Float) = 0.5
        _ShoreDepthRange ("Shore Fade Depth", Float) = 1.5
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

            TEXTURE2D(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);

            CBUFFER_START(UnityPerMaterial)
                float _AOStrength;
                float _SunLightFactor;
                float _AmbientLight;
                float _WaterAlpha;
                float _WaveAmplitude;
                float _WaveFrequency;
                float _WaveSpeed;
                float _FresnelPower;
                float _FresnelMin;
                float _FresnelMax;
                float _SpecularPower;
                float _SpecularIntensity;
                float _ShoreDepthRange;
            CBUFFER_END

            Varyings vert(uint svVertexID : SV_VertexID)
            {
                DecodedVertex dv = FetchVertex(svVertexID);

                // Wave animation on water top faces
                if (dv.fluidTop == 1)
                {
                    float3 wp = dv.positionOS;
                    float phase = wp.x * _WaveFrequency + wp.z * _WaveFrequency * 0.7 + _Time.y * _WaveSpeed;
                    float wave = sin(phase) * 0.5 + sin(phase * 2.3 + 1.7) * 0.3 + sin(phase * 0.6 + 3.1) * 0.2;
                    dv.positionOS.y += wave * _WaveAmplitude;
                }

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
                output.isWaterTop = dv.fluidTop;

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
                half lambert = (half)(ndotl * 0.75 + 0.25);

                // Voxel light modulates direct lighting; ambient is a floor independent of light level
                half3 directColor = texColor.rgb * lambert * mainLight.color.rgb * input.light;
                half3 ambientColor = texColor.rgb * (half)_AmbientLight;
                half3 finalColor = (directColor + ambientColor) * input.ao;

                // --- Water surface effects (top faces only) ---
                half alpha = (half)_WaterAlpha;

                if (input.isWaterTop == 1)
                {
                    // View direction for fresnel and specular
                    float3 viewDir = normalize(_WorldSpaceCameraPos - input.positionWS);

                    // Fresnel: more reflective at grazing angles
                    float ndotv = saturate(dot(float3(0, 1, 0), viewDir));
                    float fresnel = pow(1.0 - ndotv, _FresnelPower);
                    alpha = (half)lerp(_FresnelMin, _FresnelMax, fresnel);

                    // Sun specular highlight (Blinn-Phong)
                    float3 halfVec = normalize(viewDir + mainLight.direction);
                    float spec = pow(saturate(dot(float3(0, 1, 0), halfVec)), _SpecularPower);
                    finalColor += mainLight.color.rgb * spec * _SpecularIntensity * input.light;

                    // Shore fade: reduce alpha where water is shallow (depth-based)
                    float2 screenUV = input.positionCS.xy / _ScreenParams.xy;
                    float rawDepth = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV).r;
                    float sceneEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                    float surfaceEyeDepth = input.positionCS.w;
                    float depthDiff = sceneEyeDepth - surfaceEyeDepth;
                    float shoreFade = saturate(depthDiff / _ShoreDepthRange);
                    alpha *= (half)shoreFade;
                }

                finalColor = MixFog(finalColor, input.fogFactor);

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }
}
