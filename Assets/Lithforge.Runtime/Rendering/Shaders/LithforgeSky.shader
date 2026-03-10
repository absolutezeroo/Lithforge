Shader "Lithforge/ProceduralSky"
{
    Properties
    {
        _HorizonColor ("Horizon Color", Color) = (0.6, 0.75, 1.0, 1.0)
        _ZenithColor  ("Zenith Color", Color)  = (0.15, 0.3, 0.8, 1.0)
        _SunColor     ("Sun Color", Color)     = (1.0, 0.95, 0.8, 1.0)
        _SunDirection ("Sun Direction", Vector) = (0.5, 0.5, 0.5, 0.0)
        _SunDiscSize  ("Sun Disc Size", Float)  = 0.9985
        _SunHaloSize  ("Sun Halo Size", Float)  = 0.97
        _StarVisibility ("Star Visibility", Range(0,1)) = 0.0
        _StarDensity  ("Star Density", Float)  = 300.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Background"
            "Queue" = "Background"
            "PreviewType" = "Skybox"
        }

        Pass
        {
            Name "Sky"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _HorizonColor;
                half4 _ZenithColor;
                half4 _SunColor;
                float4 _SunDirection;
                float _SunDiscSize;
                float _SunHaloSize;
                float _StarVisibility;
                float _StarDensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 viewDirWS  : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                // Strip translation so sky renders at infinity
                float4x4 viewNoTranslation = UNITY_MATRIX_V;
                viewNoTranslation._m03 = 0;
                viewNoTranslation._m13 = 0;
                viewNoTranslation._m23 = 0;

                float4 posVS = mul(viewNoTranslation, mul(UNITY_MATRIX_M, input.positionOS));
                output.positionCS = mul(UNITY_MATRIX_P, posVS);

                // Push to far plane
                output.positionCS.z = output.positionCS.w;

                // Reconstruct world-space view direction
                output.viewDirWS = normalize(mul((float3x3)UNITY_MATRIX_I_V, normalize(posVS.xyz)));

                return output;
            }

            float StarHash(float3 p)
            {
                return frac(sin(dot(floor(p), float3(127.1, 311.7, 74.7))) * 43758.5);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 viewDir = normalize(input.viewDirWS);

                // Sky gradient: 0 at horizon, 1 at zenith
                float t = saturate(viewDir.y * 0.5 + 0.5);
                float skyBlend = pow(t, 0.5);
                half3 skyColor = lerp(_HorizonColor.rgb, _ZenithColor.rgb, skyBlend);

                // Below horizon: darken toward ground color
                if (viewDir.y < 0.0)
                {
                    float groundFade = saturate(-viewDir.y * 3.0);
                    skyColor = lerp(skyColor, _HorizonColor.rgb * 0.4, groundFade);
                }

                // Sun disc and halo
                float3 sunDir = normalize(_SunDirection.xyz);
                float sunDot = dot(viewDir, sunDir);
                float disc = smoothstep(_SunDiscSize - 0.001, _SunDiscSize, sunDot);
                float halo = smoothstep(_SunHaloSize, _SunDiscSize, sunDot) * 0.25;
                skyColor += _SunColor.rgb * (disc + halo);

                // Stars (visible when sun is down, only above horizon)
                float starBrightness = StarHash(viewDir * _StarDensity);
                starBrightness = step(0.97, starBrightness) * _StarVisibility;
                starBrightness *= saturate(viewDir.y * 2.0);
                skyColor += starBrightness;

                return half4(skyColor, 1.0h);
            }
            ENDHLSL
        }
    }
}
