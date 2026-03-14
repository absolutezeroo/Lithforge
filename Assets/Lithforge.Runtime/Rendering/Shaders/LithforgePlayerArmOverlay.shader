Shader "Lithforge/PlayerArmOverlay"
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
            "RenderType" = "TransparentCutout"
            "Queue" = "Geometry+11"
        }

        Pass
        {
            Name "ArmOverlayForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ArmVert
            #pragma fragment ArmOverlayFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "LithforgePlayerArmCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _SunLightFactor;
                float _AmbientLight;
            CBUFFER_END

            half4 ArmOverlayFrag(ArmVaryings i) : SV_Target
            {
                float2 skinUV = float2(i.uv.x, 1.0 - i.uv.y);
                half4 col = SAMPLE_TEXTURE2D(_SkinTex, sampler_SkinTex, skinUV);

                // Discard transparent overlay pixels
                clip(col.a - 0.5h);

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
            Name "ArmOverlayDepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ArmVert
            #pragma fragment ArmOverlayDepthFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "LithforgePlayerArmCommon.hlsl"

            half4 ArmOverlayDepthFrag(ArmVaryings i) : SV_Target
            {
                // Must also clip in depth-only pass to match forward pass
                float2 skinUV = float2(i.uv.x, 1.0 - i.uv.y);
                half4 col = SAMPLE_TEXTURE2D(_SkinTex, sampler_SkinTex, skinUV);
                clip(col.a - 0.5h);
                return 0;
            }
            ENDHLSL
        }
    }
}
