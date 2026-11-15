Shader "Hidden/LightningBolt"
{
    Properties
    {
        _Color ("Color", Color) = (1.6, 1.7, 2.0, 1)
        _Alpha ("Alpha", Float) = 0
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
            Name "LightningBolt"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half _Alpha;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half2 uv : TEXCOORD0;
                half strength : TEXCOORD1;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.uv = half2(v.uv);
                o.strength = v.color.a;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half p = saturate(1.0h - abs(i.uv.x * 2.0h - 1.0h));
                half k = pow(p, i.uv.y);
                return half4(_Color.rgb * (_Alpha * i.strength * k), 0.0h);
            }
            ENDHLSL
        }
    }
}
