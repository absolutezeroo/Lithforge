Shader "Lithforge/PlayerModelOverlay"
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
            Name "PlayerModelOverlayForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex PlayerVert
            #pragma fragment PlayerModelOverlayFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "LithforgePlayerModelCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _SunLightFactor;
                float _AmbientLight;
            CBUFFER_END

            half4 PlayerModelOverlayFrag(PlayerVaryings i) : SV_Target
            {
                float2 skinUV = float2(i.uv.x, 1.0 - i.uv.y);
                half4 col = SAMPLE_TEXTURE2D(_SkinTex, sampler_SkinTex, skinUV);

                // Discard transparent overlay pixels
                clip(col.a - 0.5h);

                half3 finalColor = ComputeFirstPersonLighting(
                    col.rgb, i.normalWS, _SunLightFactor, _AmbientLight);

                return half4(finalColor, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "PlayerModelOverlayDepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex PlayerVert
            #pragma fragment PlayerModelOverlayDepthFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "LithforgePlayerModelCommon.hlsl"

            half4 PlayerModelOverlayDepthFrag(PlayerVaryings i) : SV_Target
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
