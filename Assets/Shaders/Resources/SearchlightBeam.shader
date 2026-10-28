Shader "Hidden/SearchlightBeam"
{
    Properties
    {
        _Tint ("Tint", Color) = (1, 0.851, 0.627, 1)
        _Intensity ("Intensity", Float) = 1
        _Range ("Range", Float) = 100
        _SourceRadius ("Source Radius", Float) = 4
        _CosInner ("Cos Inner", Float) = 0.99
        _CosOuter ("Cos Outer", Float) = 0.95
        _Falloff ("Falloff", Float) = 1.6
        _GlowSize ("Glow Size", Float) = 2
        _GlowIntensity ("Glow Intensity", Float) = 2
        _Soft ("Soft Depth", Float) = 4
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "SearchlightBeam"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            half _Intensity;
            float _Range;
            float _SourceRadius;
            float _CosInner;
            float _CosOuter;
            half _Falloff;
            float _GlowSize;
            half _GlowIntensity;
            float _Soft;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 local : TEXCOORD0;
                float eye : TEXCOORD1;
            };

            Varyings Vert(Attributes v)
            {
                float3 origin = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                float3 axis = normalize(TransformObjectToWorldDir(float3(0.0, 0.0, 1.0)));
                float range = max(_Range, 1e-3);
                float3 p;
                float3 local;

                if (v.uv.z > 0.5)
                {
                    float3 right = UNITY_MATRIX_V[0].xyz;
                    float3 up = UNITY_MATRIX_V[1].xyz;
                    float3 toCam = normalize(_WorldSpaceCameraPos - origin);
                    p = origin + (right * v.uv.x + up * v.uv.y) * _GlowSize + toCam * _GlowSize;
                    local = float3(v.uv.xy, 1.0);
                }
                else
                {
                    float sinOuter = sqrt(saturate(1.0 - _CosOuter * _CosOuter));
                    float halfWidth = min(range, (range + _SourceRadius) * sinOuter);
                    float3 middle = origin + axis * (range * 0.5);
                    float3 side = cross(axis, normalize(_WorldSpaceCameraPos - middle));
                    float len = length(side);
                    side = len > 1e-4 ? side / len : float3(1.0, 0.0, 0.0);

                    float2 flat = float2(lerp(-_SourceRadius, range, v.uv.y), v.uv.x * halfWidth);
                    p = origin + axis * flat.x + side * flat.y;
                    local = float3(flat, 0.0);
                }

                Varyings o;
                o.positionCS = TransformWorldToHClip(p);
                o.local = local;
                o.eye = -TransformWorldToView(p).z;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 screenUV = i.positionCS.xy / _ScaledScreenParams.xy;
                float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                half soft = (half)saturate((sceneEye - i.eye) / max(_Soft, 1e-3));

                half light;
                if (i.local.z > 0.5)
                {
                    float r2 = dot(i.local.xy, i.local.xy);
                    light = (half)saturate(1.0 - r2);
                    light = light * light * light * _GlowIntensity;
                }
                else
                {
                    float2 local = i.local.xy;
                    half radial = pow((half)saturate(1.0 - length(local) / max(_Range, 1e-3)), _Falloff);
                    float2 q = local + float2(_SourceRadius, 0.0);
                    float cosA = q.x * rsqrt(max(dot(q, q), 1e-6));
                    light = radial * (half)smoothstep(_CosOuter, _CosInner, cosA);
                }

                half3 rgb = _Tint.rgb * (light * soft * _Intensity);
                return half4(rgb, 0.0h);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
