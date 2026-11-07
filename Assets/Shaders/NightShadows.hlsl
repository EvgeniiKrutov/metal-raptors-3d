#ifndef MR_NIGHT_SHADOWS_INCLUDED
#define MR_NIGHT_SHADOWS_INCLUDED

#define NL_MAX_SHADOWS 4

Texture2D _NL_ShadowMap;
float4x4 _NL_ShadowMatrix;
float4 _NL_ShadowParams;
float4 _NL_ShadowSize;
float4 _NL_ShadowRows[NL_MAX_SHADOWS];
float4 _NL_ShadowSpans[NL_MAX_SHADOWS];

half NightShadowRay(int bin, int row, float r, float reach)
{
    float4 ray = _NL_ShadowMap.Load(int3(bin, row, 0));
    float first = ray.y * saturate(r - ray.x * reach - _NL_ShadowParams.y);
    float strong = ray.w * saturate(r - ray.z * reach - _NL_ShadowParams.y);
    return 1.0h - (half)(max(first, strong) * _NL_ShadowParams.z);
}

half NightShadow(float row, float3 posWS)
{
    if (row < -0.5 || row > _NL_ShadowParams.x - 0.5) return 1.0h;

    float4 c = mul(_NL_ShadowMatrix, float4(posWS, 1.0));
    if (c.w <= 1e-4) return 1.0h;

    int index = (int)(row + 0.5);
    float4 start = _NL_ShadowRows[index];
    float4 span = _NL_ShadowSpans[index];
    float2 rel = (c.xy / c.w * 0.5 + 0.5) * _NL_ShadowSize.xy - start.xy;
    float angle = atan2(start.z * rel.y - start.w * rel.x, dot(start.zw, rel));
    float last = _NL_ShadowSize.z - 1.0;
    float bin = (angle / span.x + 0.5) * _NL_ShadowSize.z - 0.5;
    if (bin < 0.0 || bin > last) return 1.0h;

    float r = length(rel);
    float first = floor(bin);
    return lerp(NightShadowRay((int)first, index, r, span.y),
                NightShadowRay((int)min(first + 1.0, last), index, r, span.y), (half)(bin - first));
}

#endif
