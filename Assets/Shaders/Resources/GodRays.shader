// Screen-space sun shafts for the terrain levels' daytimes. The depth buffer says which
// pixels are sky and which are geometry; this radially blurs that sky mask outward from the
// sun's screen position, so the terrain, the trees and the planes carve dark gaps into the
// streaks — light striking out from behind them. Injected before post processing so bloom
// blows the shafts out with everything else. Design notes: docs/atmospheres.md.
//
// _SunScreenPos is written by GodRays.cs every frame, so it is deliberately not a Property.
// Lives in Resources so Shader.Find sees it in builds (nothing references it as an asset;
// GodRays builds the material at runtime).
Shader "Hidden/StylizedGodRays"
{
    Properties
    {
        _RayColor      ("Ray Color (HDR)",  Color)          = (1.0, 0.62, 0.35, 1)
        _Intensity     ("Intensity",        Range(0, 5))    = 1.2
        _Density       ("Density (length)", Range(0, 2))    = 0.85
        _Weight        ("Weight",           Range(0, 2))    = 0.6
        _Decay         ("Decay",            Range(0.85, 1)) = 0.97
        _SampleCount   ("Samples",          Range(8, 96))   = 48
        _RadialFalloff ("Radial Falloff",   Range(0, 4))    = 1.1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off

        Pass
        {
            Name "StylizedGodRays"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // Blit.hlsl lives in the core package from URP 17 on, not under universal/ShaderLibrary.
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // xy = sun viewport position, z = visibility 0..1 (see GodRays.cs).
            float4 _SunScreenPos;

            float4 _RayColor;
            float  _Intensity;
            float  _Density;
            float  _Weight;
            float  _Decay;
            float  _SampleCount;
            float  _RadialFalloff;

            // 1 where the pixel is sky, 0 where something occludes it.
            float SkyMask(float2 uv)
            {
                float raw = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    return step(raw, 1e-6);
                #else
                    return step(1.0 - 1e-6, raw);
                #endif
            }

            // Per-pixel jitter; kills the banding a low sample count leaves behind.
            float IGNoise(float2 p)
            {
                return frac(52.9829189 * frac(dot(p, float2(0.06711056, 0.00583715))));
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv = i.texcoord;
                half3 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;

                if (_SunScreenPos.z <= 0.001)
                    return half4(scene, 1);

                int    steps = (int)_SampleCount;
                float2 toSun = uv - _SunScreenPos.xy;
                float2 delta = toSun * (_Density / steps);

                float2 coord = uv - delta * IGNoise(i.positionCS.xy);
                float  decay = 1.0;
                float  accum = 0.0;

                UNITY_LOOP
                for (int s = 0; s < steps; s++)
                {
                    coord -= delta;
                    accum += SkyMask(saturate(coord)) * decay * _Weight;
                    decay *= _Decay;
                }
                accum /= steps;

                float falloff = saturate(1.0 - length(toSun) * _RadialFalloff);
                falloff *= falloff;

                half3 rays = _RayColor.rgb * accum * _Intensity * falloff * _SunScreenPos.z;

                return half4(scene + rays, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
