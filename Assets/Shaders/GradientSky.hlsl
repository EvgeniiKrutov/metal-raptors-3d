// The gradient sky evaluated as a plain function, shared by the skybox that draws it
// (Resources/GradientSkybox.shader) and the aerial-haze pass that has to reproduce it
// exactly on fogged geometry (Resources/AerialHaze.shader). Design notes: docs/atmospheres.md.
#ifndef MR_GRADIENT_SKY_INCLUDED
#define MR_GRADIENT_SKY_INCLUDED

struct MRSky
{
    float3 top;
    float3 horizon;
    float3 bottom;
    float3 sunColor;
    float3 sunDir;          // must be normalised by the caller
    float  horizonFalloff;
    float  horizonSlope;
    float  sunFalloff;
    float  sunIntensity;
    float  haloFalloff;
    float  haloIntensity;
    float  discRadius;
    float  discEdge;
    float  mariaIntensity;
};

float MRHash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float MRValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(MRHash21(i), MRHash21(i + float2(1, 0)), u.x),
                lerp(MRHash21(i + float2(0, 1)), MRHash21(i + float2(1, 1)), u.x), u.y);
}

// Height of a view direction above the horizon *plane* through the eye: +1 straight up,
// 0 on the horizon itself, -1 straight down. `slope` is the plane's dy/dz, so the zero set
// is a plane rather than a cone and projects to a straight, level screen line.
float MRSkyHeight(float3 d, float slope)
{
    return d.y - slope * d.z;
}

// The sky for one view direction, minus the stars (which only the skybox draws) and minus
// the exposure multiply (applied by each caller after its own additions).
float3 MRSkyColor(float3 d, MRSky s, out float above, out float discMask, out float halo)
{
    float h = MRSkyHeight(d, s.horizonSlope);
    above = pow(saturate(h), 1.0 / s.horizonFalloff);
    float below = pow(saturate(-h), 1.0 / s.horizonFalloff);

    float3 col = lerp(s.horizon, s.top, above);
    col = lerp(col, s.bottom, below);

    float sd = saturate(dot(d, s.sunDir));
    halo = pow(sd, s.haloFalloff) * s.haloIntensity;

    discMask = 0.0;
    if (s.discRadius > 0.0)
    {
        float R = radians(s.discRadius);
        float edge = radians(s.discEdge);
        float ang = acos(clamp(dot(d, s.sunDir), -1.0, 1.0));
        discMask = 1.0 - smoothstep(R - edge, R + edge, ang);

        if (discMask > 0.0)
        {
            float3 mR = normalize(cross(float3(0, 1, 0), s.sunDir));
            float3 mU = cross(s.sunDir, mR);
            float2 uv = float2(dot(d, mR), dot(d, mU)) / sin(R);
            float limb = 1.0 - 0.18 * saturate(dot(uv, uv));
            float n = 0.6 * MRValueNoise(uv * 2.6 + 17.0)
                    + 0.4 * MRValueNoise(uv * 5.2 + 31.0);
            float maria = 1.0 - s.mariaIntensity * smoothstep(0.35, 0.8, n);
            col = lerp(col, s.sunColor * s.sunIntensity * limb * maria, discMask);
        }
    }
    else
    {
        col += s.sunColor * pow(sd, s.sunFalloff) * s.sunIntensity;
    }

    col += s.sunColor * halo;
    return col;
}

#endif
