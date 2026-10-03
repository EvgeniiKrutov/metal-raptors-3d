Shader "Hidden/NightLightAir"
{
    Properties
    {
        _NightHaze ("Night Haze", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-10"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "NightLightAir"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../NightLighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float _NightHaze;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half valid;
                float3 planePos = NightPlanePoint(_WorldSpaceCameraPos, i.positionWS - _WorldSpaceCameraPos, valid);
                half3 air = NightLampAirShadowed(planePos) * (half)(_NightHaze * _NightBlend) * valid;
                return half4(air, 0.0h);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
