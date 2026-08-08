// Aerial perspective: fades the land into the *sky* rather than into one flat fog colour, by
// adding back what Unity's linear fog leaves out —
//
//     scene += fogWeight * (skyColor(viewRay) - fogColor)
//
// which turns lerp(scene, fogColor, w) into lerp(scene, skyColor, w). Additive, so it never
// reads the colour target. Design notes: docs/atmospheres.md.
//
// Lives in Resources so Shader.Find sees it in builds (AerialHaze.cs builds the material at
// runtime; nothing references it as an asset).
Shader "Hidden/AerialHaze"
{
    Properties
    {
        _TopColor      ("Zenith Color", Color)        = (0.30, 0.45, 0.75, 1)
        _HorizonColor  ("Horizon Color", Color)       = (1.00, 0.62, 0.35, 1)
        _BottomColor   ("Below Horizon Color", Color) = (0.85, 0.45, 0.30, 1)
        _SunColor      ("Sun Color", Color)           = (1.0, 0.9, 0.7, 1)
        _HazeFogColor  ("Fog Color", Color)           = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off
        Blend One One, Zero One

        Pass
        {
            Name "AerialHaze"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // Blit.hlsl lives in the core package from URP 17 on, not under universal/ShaderLibrary.
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "../GradientSky.hlsl"

            float4 _TopColor, _HorizonColor, _BottomColor, _SunColor, _HazeFogColor;
            float  _HorizonFalloff, _HorizonSlope, _Exposure;
            float4 _SunDirection;
            float  _SunFalloff, _SunIntensity;
            float  _HaloFalloff, _HaloIntensity;
            float  _DiscRadius, _DiscEdge, _MariaIntensity;

            // x = fog start, y = fog end, z = 1 / (end - start). Written by AerialHaze.cs.
            float4 _HazeFogRange;
            // The view ray at viewport (0,0) and its spans across the frame, so a direction
            // is a plain bilinear blend. Written by AerialHaze.cs.
            float4 _HazeRayCorner, _HazeRayRight, _HazeRayUp;

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv = i.texcoord;

                // Sky pixels already are the sky; adding the difference again would double it.
                float raw = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    if (raw <= 1e-6) return half4(0.0, 0.0, 0.0, 0.0);
                #else
                    if (raw >= 1.0 - 1e-6) return half4(0.0, 0.0, 0.0, 0.0);
                #endif

                float eye = LinearEyeDepth(raw, _ZBufferParams);
                float fog = saturate((eye - _HazeFogRange.x) * _HazeFogRange.z);
                if (fog <= 0.0) return half4(0.0, 0.0, 0.0, 0.0);

                float3 d = normalize(_HazeRayCorner.xyz
                                   + _HazeRayRight.xyz * uv.x
                                   + _HazeRayUp.xyz * uv.y);

                MRSky s;
                s.top            = _TopColor.rgb;
                s.horizon        = _HorizonColor.rgb;
                s.bottom         = _BottomColor.rgb;
                s.sunColor       = _SunColor.rgb;
                s.sunDir         = normalize(_SunDirection.xyz);
                s.horizonFalloff = _HorizonFalloff;
                s.horizonSlope   = _HorizonSlope;
                s.sunFalloff     = _SunFalloff;
                s.sunIntensity   = _SunIntensity;
                s.haloFalloff    = _HaloFalloff;
                s.haloIntensity  = _HaloIntensity;
                s.discRadius     = _DiscRadius;
                s.discEdge       = _DiscEdge;
                s.mariaIntensity = _MariaIntensity;

                float above, discMask, halo;
                float3 sky = MRSkyColor(d, s, above, discMask, halo) * _Exposure;

                // Clamped to a brightening: geometry can only be seen above the horizon band
                // when it is close enough for fog to be near zero anyway, and an additive pass
                // cannot subtract safely from an unsigned colour target.
                return half4(max(0.0, (sky - _HazeFogColor.rgb) * fog), 0.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
