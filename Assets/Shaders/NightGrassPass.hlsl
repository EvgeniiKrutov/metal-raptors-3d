#ifndef MR_NIGHT_GRASS_PASS_INCLUDED
#define MR_NIGHT_GRASS_PASS_INCLUDED

#include "NightLighting.hlsl"

half4 NightLitPassFragmentGrass(GrassVertexOutput input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    SurfaceData surfaceData;
    InitializeSimpleLitSurfaceData(input, surfaceData);

    InputData inputData;
    InitializeInputData(input, inputData);
    SETUP_DEBUG_TEXTURE_DATA_FOR_TEX(inputData, input.uv, _MainTex);

    half4 color = UniversalFragmentBlinnPhong(inputData, surfaceData);

#if defined(_NIGHT_LIGHTING)
    half moonShadow = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask).shadowAttenuation;
    color.rgb = NightShadeLitShadowed(color.rgb, surfaceData.albedo, inputData.normalWS, inputData.positionWS, 1.0h, moonShadow);
#endif

    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    return half4(color.rgb, OutputAlpha(surfaceData.alpha, IsSurfaceTypeTransparent(_Surface)));
}

#endif
