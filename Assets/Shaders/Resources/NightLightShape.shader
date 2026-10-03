Shader "Hidden/NightLightShape"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "NightLightShape"
            Blend One One
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #define NL_MAX_LIGHTS 32

            float4 _NL_A[NL_MAX_LIGHTS];
            float4 _NL_B[NL_MAX_LIGHTS];
            float4 _NL_C[NL_MAX_LIGHTS];
            float4 _NL_D[NL_MAX_LIGHTS];
            float4x4 _NL_MaskVP;
            float _NL_MaskEncode;
            float _NL_DepthRange;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 local : TEXCOORD0;
                nointerpolation float4 shape : TEXCOORD1;
                nointerpolation float4 color : TEXCOORD2;
                nointerpolation float falloff : TEXCOORD3;
            };

            half NL_Attenuation(float2 local, float4 b, float isCone, half falloff)
            {
                float dist = length(local);
                half radial = pow(saturate(1.0 - dist / b.x), falloff);
                if (isCone < 0.5) return radial;
                float2 p = local + float2(b.w, 0.0);
                float cosA = p.x * rsqrt(max(dot(p, p), 1e-6));
                return radial * smoothstep(b.z, b.y, cosA);
            }

            float NL_DepthExtent(float2 local, float4 b, float isCone)
            {
                float range = b.x;
                if (isCone < 0.5) return sqrt(max(range * range - dot(local, local), 0.0));
                float tanOuter = sqrt(saturate(1.0 - b.z * b.z)) / max(b.z, 1e-4);
                float edge = min(max(local.x + b.w, 0.0) * tanOuter, sqrt(max(range * range - local.x * local.x, 0.0)));
                return sqrt(max(edge * edge - local.y * local.y, 0.0));
            }

            Varyings Vert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                float4 a = _NL_A[instanceID];
                float4 b = _NL_B[instanceID];
                float4 c = _NL_C[instanceID];
                float4 d = _NL_D[instanceID];

                float2 corner = float2(
                    (vertexID == 1 || vertexID == 2 || vertexID == 4) ? 1.0 : 0.0,
                    (vertexID == 2 || vertexID == 4 || vertexID == 5) ? 1.0 : 0.0);

                float range = max(b.x, 1e-3);
                float2 local;
                float2 world;

                if (c.w > 0.5)
                {
                    float sinOuter = sqrt(saturate(1.0 - b.z * b.z));
                    float halfWidth = min(range, (range + b.w) * sinOuter);
                    local = float2(lerp(-b.w, range, corner.x), lerp(-halfWidth, halfWidth, corner.y));
                    float2 side = float2(-a.w, a.z);
                    world = a.xy + a.zw * local.x + side * local.y;
                }
                else
                {
                    local = lerp(-range, range, corner);
                    world = a.xy + local;
                }

                Varyings o;
                o.positionCS = mul(_NL_MaskVP, float4(world, 0.0, 1.0));
                o.local = local;
                o.shape = float4(range, b.yzw);
                o.color = c;
                o.falloff = d.x;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half att = NL_Attenuation(i.local, i.shape, i.color.w, (half)i.falloff);
                float3 rgb = i.color.rgb * att * _NL_MaskEncode;
                float lum = dot(rgb, float3(0.2126, 0.7152, 0.0722));
                float depth = saturate(NL_DepthExtent(i.local, i.shape, i.color.w) / max(_NL_DepthRange, 1e-3));
                return half4(rgb, lum * depth);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
