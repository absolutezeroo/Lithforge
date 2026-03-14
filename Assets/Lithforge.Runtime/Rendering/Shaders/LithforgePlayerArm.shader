Shader "Lithforge/PlayerArm"
{
    Properties
    {
        _SunLightFactor ("Sun Light Factor", Range(0, 1)) = 1.0
        _AmbientLight ("Ambient Light", Range(0, 1)) = 0.2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry+10"
        }

        Pass
        {
            Name "ArmForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ArmVert
            #pragma fragment ArmFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "LithforgePlayerArmCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _SunLightFactor;
                float _AmbientLight;
            CBUFFER_END

            half4 ArmFrag(ArmVaryings i) : SV_Target
            {
                // Flip V because Unity loads PNG with bottom-left origin,
                // but skin UVs use top-left origin
                float2 skinUV = float2(i.uv.x, 1.0 - i.uv.y);
                half4 col = SAMPLE_TEXTURE2D(_SkinTex, sampler_SkinTex, skinUV);

                // Simple directional lighting matching voxel shader style
                Light mainLight = GetMainLight();
                float ndotl = saturate(dot(i.normalWS, mainLight.direction));
                half lambert = (half)(ndotl * 0.6 + 0.4);

                half sunFactor = (half)_SunLightFactor;
                half3 directColor = col.rgb * lambert * mainLight.color.rgb * sunFactor;
                half3 ambientColor = col.rgb * (half)_AmbientLight;
                half3 finalColor = directColor + ambientColor;

                return half4(finalColor, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ArmDepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ArmVert
            #pragma fragment ArmDepthFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "LithforgePlayerArmCommon.hlsl"

            half4 ArmDepthFrag(ArmVaryings i) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
