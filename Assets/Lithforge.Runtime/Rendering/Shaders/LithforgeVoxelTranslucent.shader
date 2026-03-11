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
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma require 2darray

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D_ARRAY(_AtlasArray);
            SAMPLER(sampler_AtlasArray);

            CBUFFER_START(UnityPerMaterial)
                float _AOStrength;
                float _SunLightFactor;
                float _AmbientLight;
                float _WaterAlpha;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;  // r=AO, g=blockLight, b=sunLight, a=texIndex
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                nointerpolation int texIndex : TEXCOORD1;
                half ao : TEXCOORD2;
                half light : TEXCOORD3;
                float3 normalWS : TEXCOORD4;
                float fogFactor : TEXCOORD5;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS);
                output.positionCS = vertexInput.positionCS;

                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
                output.normalWS = normalInput.normalWS;

                // Texture array index from color.a (stored as half, lossless up to 2048)
                output.uv = input.uv;
                output.texIndex = (int)round(input.color.a);

                // AO: color.r is ao/3 (0=fully occluded, 1=unoccluded)
                half aoNorm = input.color.r;
                output.ao = lerp(1.0h, aoNorm, (half)_AOStrength);

                // Lighting: block light (g) and sun light (b)
                // Ambient is applied separately in the fragment shader
                half blockLight = input.color.g;
                half sunLight = input.color.b * (half)_SunLightFactor;
                output.light = max(blockLight, sunLight);

                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Tile UV for greedy quads (frac wraps the texture)
                float2 tiledUV = frac(input.uv);

                half4 texColor = SAMPLE_TEXTURE2D_ARRAY(
                    _AtlasArray, sampler_AtlasArray, tiledUV, input.texIndex);

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
