Shader "Hidden/RainStreaks"
{
    Properties
    {
        _Color ("Color", Color) = (0.80, 0.83, 0.88, 1)
        _Depth ("Depth", Vector) = (200, 400, 0.002, 0.3)
        _Motion ("Motion", Vector) = (-300, -1000, 0.035, 0.15)
        _View ("View", Vector) = (0.7, 0.4, 1.12, 0)
        _Eye ("Eye", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "RainStreaks"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            float4 _Depth;
            float4 _Motion;
            float4 _View;
            float4 _Eye;
            CBUFFER_END

            float _RainTime;
            half _StormFlash;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 seed : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half2 uv : TEXCOORD0;
                half alpha : TEXCOORD1;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                float4 s = v.seed;

                float d = lerp(_Depth.x, _Depth.y, s.z);
                float2 halfBox = d * _View.xy * _View.z;
                float2 box = halfBox * 2.0;

                float pace = lerp(1.0 - _Motion.w, 1.0 + _Motion.w, s.w);
                float2 vel = _Motion.xy * pace;
                float2 p = frac(s.xy + vel * (_RainTime / box)) * box - halfBox;

                float2 dir = normalize(vel);
                float2 side = float2(-dir.y, dir.x);
                float len = length(vel) * _Motion.z;

                float pixel = 2.0 * d * _View.y / max(_ScreenParams.y, 1.0);
                float width = d * _Depth.z;
                float drawn = max(width, pixel);

                float2 offset = -dir * (v.positionOS.y * len) + side * (v.positionOS.x * drawn);
                float3 world = float3(_Eye.x + p.x + offset.x, _Eye.y + p.y + offset.y, _Eye.z + d);
                o.positionCS = TransformWorldToHClip(world);

                o.uv = half2(v.positionOS.x * 2.0, v.positionOS.y);
                float edge = saturate((halfBox.y - abs(p.y)) / (halfBox.y * 0.1));
                o.alpha = _Depth.w * (width / drawn) * edge * lerp(0.55, 1.0, s.w);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half across = 1.0h - i.uv.x * i.uv.x;
                half along = 1.0h - i.uv.y;
                half a = i.alpha * across * along;
                half3 col = _Color.rgb * (1.0h + _StormFlash * 2.0h);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}
