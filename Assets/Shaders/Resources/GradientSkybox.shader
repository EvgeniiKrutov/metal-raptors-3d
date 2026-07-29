// Painterly gradient skybox for the terrain levels' daytimes: a vertical lerp from a cool
// zenith down to a warm horizon band, with a two-part sun — a soft bright core plus a wide
// atmospheric halo — so a low sun reads as a glowing disc in mist rather than a hard ball.
// The night extensions (opaque moon disc, procedural stars) default to no-ops; design
// notes in docs/atmospheres.md. Lives in Resources so Shader.Find sees it in builds
// (nothing references it as an asset; the sky classes build materials at runtime).
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
        // View-direction Y where the horizon band centres (0 = eye level). SkyHorizon feeds
        // the direction toward the map's fogged far edge here, so the band and a setting sun
        // sit at the land's visible edge instead of at infinity's eye-level horizon.
        _HorizonLevel  ("Horizon Level (dir Y)", Range(-1, 1)) = 0
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

            struct appdata { float4 vertex : POSITION; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            fixed4 _TopColor, _HorizonColor, _BottomColor, _SunColor;
            float  _HorizonFalloff, _HorizonLevel, _Exposure;
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

            float hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash21(i), hash21(i + float2(1, 0)), u.x),
                            lerp(hash21(i + float2(0, 1)), hash21(i + float2(1, 1)), u.x), u.y);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float  h = d.y;                        // -1 = down, +1 = up

                // Vertical gradient: horizon -> zenith above, horizon -> below-color under,
                // centred on _HorizonLevel and rescaled so zenith/nadir still reach 1
                float tUp   = pow(saturate((h - _HorizonLevel) / (1.0 - _HorizonLevel)), 1.0 / _HorizonFalloff);
                float tDown = pow(saturate((_HorizonLevel - h) / (1.0 + _HorizonLevel)), 1.0 / _HorizonFalloff);
                fixed3 col  = lerp(_HorizonColor.rgb, _TopColor.rgb, tUp);
                col         = lerp(col, _BottomColor.rgb, tDown);

                // Sun (or moon): with _DiscRadius = 0, the day skies' additive soft core;
                // with a radius, an opaque hard-edged disc — limb-shaded, with noise-dark
                // maria patches — that reads as a solid body rather than a glow. The wide
                // halo is added on top either way (the light scattered around the body).
                float3 sunDir = normalize(_SunDirection.xyz);
                float  sd     = saturate(dot(d, sunDir));
                float  halo   = pow(sd, _HaloFalloff) * _HaloIntensity;

                float discMask = 0.0;
                if (_DiscRadius > 0.0)
                {
                    float R    = radians(_DiscRadius);
                    float edge = radians(_DiscEdge);
                    float ang  = acos(clamp(dot(d, sunDir), -1.0, 1.0));
                    discMask   = 1.0 - smoothstep(R - edge, R + edge, ang);

                    if (discMask > 0.0)
                    {
                        float3 mR   = normalize(cross(float3(0, 1, 0), sunDir));
                        float3 mU   = cross(sunDir, mR);
                        float2 uv   = float2(dot(d, mR), dot(d, mU)) / sin(R);
                        float  limb = 1.0 - 0.18 * saturate(dot(uv, uv));
                        float  n    = 0.6 * vnoise(uv * 2.6 + 17.0)
                                    + 0.4 * vnoise(uv * 5.2 + 31.0);
                        float  maria = 1.0 - _MariaIntensity * smoothstep(0.35, 0.8, n);
                        float3 moon  = _SunColor.rgb * _SunIntensity * limb * maria;
                        col = lerp(col, moon, discMask);
                    }
                }
                else
                {
                    col += _SunColor.rgb * pow(sd, _SunFalloff) * _SunIntensity;
                }

                // Stars: one faint-to-bright point per hash cell, kept off the horizon band
                // (tUp), the disc, and the moonglow patch, with a slow subtle twinkle.
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
                    float  mask = saturate(tUp * 2.5) * (1.0 - discMask) * saturate(1.0 - halo * 4.0);
                    float3 starCol = lerp(float3(0.75, 0.82, 1.0), float3(1.0, 0.95, 0.85), rnd.z);
                    col += starCol * (star * b * tw * _StarIntensity * mask);
                }

                col += _SunColor.rgb * halo;

                col *= _Exposure;
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
