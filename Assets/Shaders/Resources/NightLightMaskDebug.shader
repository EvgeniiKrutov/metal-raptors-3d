Shader "Hidden/NightLightMaskDebug"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off
        Blend SrcAlpha OneMinusSrcAlpha, Zero One

        Pass
        {
            Name "NightLightMaskDebug"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "../NightLighting.hlsl"

            float4 _NL_RayOrigin;
            float4 _NL_RayCorner;
            float4 _NL_RayRight;
            float4 _NL_RayUp;

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv = i.texcoord;
                float3 dir = _NL_RayCorner.xyz + _NL_RayRight.xyz * uv.x + _NL_RayUp.xyz * uv.y;
                half valid;
                float3 planePos = NightPlanePoint(_NL_RayOrigin.xyz, dir, valid);
                half3 mask = NightLampAir(planePos) * valid;
                return half4(mask, 0.5h);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
