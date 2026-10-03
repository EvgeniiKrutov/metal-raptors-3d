#ifndef MR_NIGHT_LIGHTING_INCLUDED
#define MR_NIGHT_LIGHTING_INCLUDED

Texture2D _NightLightMask;
SamplerState sampler_NightLightMask;
float4x4 _NL_MaskUVMatrix;
float _NL_MaskDecode;
float _NL_PlaneZ;
float _NL_DepthRange;

Texture2D _NL_ShadowMap;
float4x4 _NL_ShadowMatrix;
float4 _NL_ShadowParams;
float4 _NL_ShadowLight;
float4 _NL_ShadowSize;

float _NightBlend;
float3 _NightSkyTop;
float3 _NightSkyHorizon;
float3 _NightAmbientSky;
float3 _NightAmbientGround;
float3 _NightMoonColor;
float3 _NightMoonDir;
float _NightMoonShadow;
float _NightDesaturation;
float3 _NightUnlitAmbient;
float3 _NightRimColor;
float _NightRimPower;

half4 NightLampSample(float2 planeXY)
{
    float4 c = mul(_NL_MaskUVMatrix, float4(planeXY, 0.0, 1.0));
    float2 uv = c.xy / max(c.w, 1e-4) * 0.5 + 0.5;
    float2 inside = step(abs(uv - 0.5), 0.5);
    return (half4)_NightLightMask.Sample(sampler_NightLightMask, uv) * (half)(inside.x * inside.y);
}

float3 NightPlanePoint(float3 origin, float3 dir, out half valid)
{
    float dz = dir.z;
    float t = (_NL_PlaneZ - origin.z) / (abs(dz) > 1e-4 ? dz : 1e-4);
    valid = (half)(step(1e-4, abs(dz)) * step(0.0, t));
    return origin + dir * max(t, 0.0);
}

half3 NightLampAir(float3 planePosWS)
{
    return NightLampSample(planePosWS.xy).rgb * (half)_NL_MaskDecode;
}

half3 NightLamp(float3 posWS)
{
    half4 m = NightLampSample(posWS.xy);
    float lum = dot((float3)m.rgb, float3(0.2126, 0.7152, 0.0722));
    float extent = m.a / max(lum, 1e-5) * _NL_DepthRange;
    float t = abs(posWS.z - _NL_PlaneZ) / max(extent, 1e-3);
    half depth = 1.0h - (half)smoothstep(0.4, 1.0, t);
    return m.rgb * (half)_NL_MaskDecode * depth;
}

half NightShadowRay(float bin, float r)
{
    float2 ray = _NL_ShadowMap.Load(int3((int)bin, 0, 0)).rg;
    float exitR = ray.x * _NL_ShadowParams.w;
    return 1.0h - (half)(ray.y * saturate(r - exitR - _NL_ShadowParams.y));
}

half NightLampShadow(float3 posWS)
{
    if (_NL_ShadowParams.x < 0.5) return 1.0h;

    float4 c = mul(_NL_ShadowMatrix, float4(posWS, 1.0));
    if (c.w <= 1e-4) return 1.0h;

    float2 rel = (c.xy / c.w * 0.5 + 0.5) * _NL_ShadowSize.xy - _NL_ShadowLight.xy;
    float2 axis = _NL_ShadowLight.zw;
    float angle = atan2(axis.x * rel.y - axis.y * rel.x, dot(axis, rel));
    float last = _NL_ShadowSize.z - 1.0;
    float bin = (angle / _NL_ShadowParams.z + 0.5) * _NL_ShadowSize.z - 0.5;
    if (bin < 0.0 || bin > last) return 1.0h;

    float r = length(rel);
    float first = floor(bin);
    return lerp(NightShadowRay(first, r), NightShadowRay(min(first + 1.0, last), r), (half)(bin - first));
}

half3 NightLampAirShadowed(float3 planePosWS)
{
    half3 air = NightLampAir(planePosWS);
    UNITY_BRANCH
    if (air.r + air.g + air.b > 1e-3h) air *= NightLampShadow(planePosWS);
    return air;
}

half3 NightLampShadowed(float3 posWS)
{
    half3 lamp = NightLamp(posWS);
    UNITY_BRANCH
    if (lamp.r + lamp.g + lamp.b > 1e-3h) lamp *= NightLampShadow(posWS);
    return lamp;
}

half3 NightGrade(half3 c)
{
    half luma = dot(c, half3(0.299h, 0.587h, 0.114h));
    return lerp(c, luma.xxx, (half)_NightDesaturation);
}

half3 NightColorLitShadowed(half3 albedo, half3 n, float3 posWS, half response, half shadow)
{
    half3 amb = lerp((half3)_NightAmbientGround, (half3)_NightAmbientSky, n.y * 0.5h + 0.5h);
    half moon = saturate(dot(n, (half3)_NightMoonDir) * 0.5h + 0.5h)
              * lerp(1.0h, shadow, (half)_NightMoonShadow);
    return NightGrade(albedo) * (amb + (half3)_NightMoonColor * moon)
         + albedo * NightLampShadowed(posWS) * response;
}

half3 NightColorLit(half3 albedo, half3 n, float3 posWS, half response)
{
    return NightColorLitShadowed(albedo, n, posWS, response, 1.0h);
}

half3 NightShadeLitShadowed(half3 dayColor, half3 albedo, half3 n, float3 posWS, half response, half shadow)
{
#if defined(_NIGHT_LIGHTING)
    return lerp(dayColor, NightColorLitShadowed(albedo, n, posWS, response, shadow), (half)_NightBlend);
#else
    return dayColor;
#endif
}

half3 NightShadeLit(half3 dayColor, half3 albedo, half3 n, float3 posWS, half response)
{
    return NightShadeLitShadowed(dayColor, albedo, n, posWS, response, 1.0h);
}

half3 NightShadeUnlit(half3 dayColor, float3 posWS, half response)
{
#if defined(_NIGHT_LIGHTING)
    half3 night = NightGrade(dayColor) * (half3)_NightUnlitAmbient
                + dayColor * NightLamp(posWS) * response;
    return lerp(dayColor, night, (half)_NightBlend);
#else
    return dayColor;
#endif
}

half3 NightShadeSky(half3 daySky, half3 nightSky, float3 posWS, half haze)
{
#if defined(_NIGHT_LIGHTING)
    return lerp(daySky, nightSky + NightLampAir(posWS) * haze, (half)_NightBlend);
#else
    return daySky;
#endif
}

half3 NightRim(half3 n, half3 viewDirWS)
{
#if defined(_NIGHT_LIGHTING)
    half rim = pow(1.0h - saturate(dot(n, viewDirWS)), (half)_NightRimPower);
    return rim * (half3)_NightRimColor * (half)_NightBlend;
#else
    return half3(0.0h, 0.0h, 0.0h);
#endif
}

void NightShadeLit_float(float3 DayColor, float3 Albedo, float3 Normal, float3 PositionWS, float Response, out float3 Out)
{
    Out = NightShadeLit(DayColor, Albedo, Normal, PositionWS, Response);
}

void NightShadeLit_half(half3 DayColor, half3 Albedo, half3 Normal, float3 PositionWS, half Response, out half3 Out)
{
    Out = NightShadeLit(DayColor, Albedo, Normal, PositionWS, Response);
}

void NightColorLit_float(float3 Albedo, float3 Normal, float3 PositionWS, float Response, out float3 Out)
{
    Out = NightColorLit(Albedo, Normal, PositionWS, Response);
}

void NightColorLit_half(half3 Albedo, half3 Normal, float3 PositionWS, half Response, out half3 Out)
{
    Out = NightColorLit(Albedo, Normal, PositionWS, Response);
}

void NightShadeUnlit_float(float3 DayColor, float3 PositionWS, float Response, out float3 Out)
{
    Out = NightShadeUnlit(DayColor, PositionWS, Response);
}

void NightShadeUnlit_half(half3 DayColor, float3 PositionWS, half Response, out half3 Out)
{
    Out = NightShadeUnlit(DayColor, PositionWS, Response);
}

void NightShadeSky_float(float3 DaySky, float3 NightSky, float3 PositionWS, float Haze, out float3 Out)
{
    Out = NightShadeSky(DaySky, NightSky, PositionWS, Haze);
}

void NightShadeSky_half(half3 DaySky, half3 NightSky, float3 PositionWS, half Haze, out half3 Out)
{
    Out = NightShadeSky(DaySky, NightSky, PositionWS, Haze);
}

void NightRim_float(float3 Normal, float3 ViewDirWS, out float3 Out)
{
    Out = NightRim(Normal, ViewDirWS);
}

void NightRim_half(half3 Normal, half3 ViewDirWS, out half3 Out)
{
    Out = NightRim(Normal, ViewDirWS);
}

#endif
