Shader "Hidden/WreckSoot"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _Seed ("Seed", Float) = 0
        _Soot ("Soot", Float) = 1
        _Dim ("Dim", Float) = 0.8
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "WreckSoot"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
            float _Seed;
            half _Soot;
            half _Dim;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash(i);
                float b = Hash(i + float2(1.0, 0.0));
                float c = Hash(i + float2(0.0, 1.0));
                float d = Hash(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float Fbm(float2 p)
            {
                float sum = 0.0;
                float amp = 0.5;
                for (int k = 0; k < 5; k++)
                {
                    sum += amp * Noise(p);
                    p = p * 2.03 + 17.1;
                    amp *= 0.5;
                }
                return sum;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

                float n = Fbm(input.uv * 7.0 + _Seed);
                float grain = Noise(input.uv * 90.0 + _Seed * 3.1) - 0.5;
                float v = n + grain * 0.12;

                half soot = smoothstep(0.43, 0.66, v) * _Soot;
                half scorch = smoothstep(0.3, 0.45, v) * (1.0 - soot);

                half grey = dot(c.rgb, half3(0.299, 0.587, 0.114));
                half3 burnt = lerp(c.rgb, grey * half3(0.82, 0.74, 0.66), 0.35) * _Dim;
                burnt = lerp(burnt, burnt * half3(0.55, 0.42, 0.3), scorch * 0.7);
                half3 col = lerp(burnt, half3(0.035, 0.03, 0.028), soot);
                return half4(col, c.a);
            }
            ENDHLSL
        }
    }
}
