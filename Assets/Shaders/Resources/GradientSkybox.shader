// Painterly gradient skybox for the terrain levels' daytimes: a vertical lerp from a cool
// zenith down to a warm horizon band, with a two-part sun — a soft bright core plus a wide
// atmospheric halo — so a low sun reads as a glowing disc in mist rather than a hard ball.
// The night extensions (opaque moon disc, procedural stars) default to no-ops; design
// notes in docs/atmospheres.md. Lives in Resources so Shader.Find sees it in builds
// (nothing references it as an asset; the sky classes build materials at runtime).
//
// The gradient and sun themselves live in ../GradientSky.hlsl, because AerialHaze.shader
// has to evaluate the identical sky to fog the land into it without a seam.
//
// Written in the Built-in (CG) style: the skybox pass is pipeline-agnostic, so this
// renders fine under URP as RenderSettings.skybox.
Shader "Custom/GradientSkybox"
{
    Properties
    {
        [Header(Gradient)]
        _TopColor      ("Zenith Color", Color)        = (0.30, 0.45, 0.75, 1)
        _HorizonColor  ("Horizon Color", Color)       = (1.00, 0.62, 0.35, 1)
        _BottomColor   ("Below Horizon Color", Color) = (0.85, 0.45, 0.30, 1)
        _HorizonFalloff("Horizon Falloff", Range(0.1, 8)) = 2.0
        // dy/dz of the plane the horizon band sits on (0 = eye level). SkyHorizon feeds the
        // slope toward the map's fogged far edge here, so the band and a setting sun sit at
        // the land's visible edge instead of at infinity's eye-level horizon. A slope is a
        // plane and projects to a straight, level screen line; a view-direction Y would be a
        // cone and would sag toward the frame edges.
        _HorizonSlope  ("Horizon Slope (dy per dz)", Range(-1, 1)) = 0
        _Exposure      ("Exposure", Range(0, 4))      = 1.0

        [Header(Sun)]
        _SunColor      ("Sun Color", Color)           = (1.0, 0.9, 0.7, 1)
        _SunDirection  ("Sun Direction", Vector)      = (0.3, 0.15, 0.5, 0)
        _SunFalloff    ("Sun Glow Tightness", Range(1, 2000)) = 60
        _SunIntensity  ("Sun Intensity", Range(0, 5)) = 1.5
        _HaloFalloff   ("Halo Tightness", Range(1, 50)) = 6
        _HaloIntensity ("Halo Intensity", Range(0, 2))  = 0.5

        // Night extensions (defaults are no-ops, so the day skies are unaffected).
        [Header(Moon Disc)]
        _DiscRadius    ("Disc Radius (deg, 0 = soft sun)", Range(0, 10)) = 0
        _DiscEdge      ("Disc Edge Width (deg)", Range(0.01, 2)) = 0.12
        _MariaIntensity("Disc Surface Patches", Range(0, 1)) = 0

        [Header(Stars)]
        _StarIntensity ("Star Intensity", Range(0, 2)) = 0
        _StarScale     ("Star Density", Range(10, 200)) = 80
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "../GradientSky.hlsl"

            struct appdata { float4 vertex : POSITION; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            fixed4 _TopColor, _HorizonColor, _BottomColor, _SunColor;
            float  _HorizonFalloff, _HorizonSlope, _Exposure;
            float4 _SunDirection;
            float  _SunFalloff, _SunIntensity;
            float  _HaloFalloff, _HaloIntensity;
            float  _DiscRadius, _DiscEdge, _MariaIntensity;
            float  _StarIntensity, _StarScale;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;   // skybox verts double as view direction
                return o;
            }

            float hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float3 hash33(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.xxy + p.yxx) * p.zyx);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);

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
                float3 col = MRSkyColor(d, s, above, discMask, halo);

                // Stars: one faint-to-bright point per hash cell, kept off the horizon band
                // (above), the disc, and the moonglow patch, with a slow subtle twinkle.
                if (_StarIntensity > 0.0)
                {
                    float3 sp   = d * _StarScale;
                    float3 cell = floor(sp);
                    float3 rnd  = hash33(cell);
                    float  dist  = length(sp - (cell + 0.2 + 0.6 * rnd));
                    float  spark = 1.0 - smoothstep(0.0, 0.18, dist);
                    float  star  = spark * spark * step(0.72, hash13(cell + 3.3));
                    float  b    = 0.35 + 0.65 * pow(hash13(cell + 7.7), 4.0);
                    float  tw   = 0.85 + 0.15 * sin(_Time.y * (1.5 + 2.5 * rnd.x) + rnd.y * 6.2832);
                    float  mask = saturate(above * 2.5) * (1.0 - discMask) * saturate(1.0 - halo * 4.0);
                    float3 starCol = lerp(float3(0.75, 0.82, 1.0), float3(1.0, 0.95, 0.85), rnd.z);
                    col += starCol * (star * b * tw * _StarIntensity * mask);
                }

                col *= _Exposure;
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
