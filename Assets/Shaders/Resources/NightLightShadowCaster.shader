Shader "Hidden/NightLightShadowCaster"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "NightLightOccupancy"
            Blend One One
            BlendOp Max
            ZWrite Off
            ZTest Always
            Cull Off
            ColorMask RG

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4x4 _NL_CasterVP;
            float4 _NL_CasterLight;
            float4 _NL_CasterCone;

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
                o.positionCS = mul(_NL_CasterVP, float4(o.positionWS, 1.0));
                return o;
            }

            float4 Frag(Varyings i) : SV_Target
            {
                float2 rel = i.positionWS.xy - _NL_CasterLight.xy;
                float along = dot(rel, _NL_CasterCone.xy);
                float side = dot(rel, float2(-_NL_CasterCone.y, _NL_CasterCone.x));
                float range = _NL_CasterCone.w;
                float edge = min(max(along + _NL_CasterLight.w, 0.0) * _NL_CasterCone.z,
                                 sqrt(max(range * range - along * along, 0.0)));
                float extent = sqrt(max(edge * edge - side * side, 0.0));
                float t = abs(i.positionWS.z - _NL_CasterLight.z) / max(extent, 1e-3);
                float lit = (1.0 - smoothstep(0.4, 1.0, t)) * step(0.0, along) * step(1e-3, extent);
                clip(lit - 0.02);
                return float4(1.0, lit, 0.0, 0.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "NightLightShadowRays"
            Blend Off
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            Texture2D _NL_Occupancy;
            SamplerState sampler_NL_Occupancy;
            float4 _NL_OccupancySize;
            float4 _NL_RayStart;
            float4 _NL_RayParams;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings o;
                float2 uv = float2((vertexID << 1) & 2, vertexID & 2);
                o.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                return o;
            }

            float4 Frag(Varyings i) : SV_Target
            {
                float angle = (i.positionCS.x * _NL_RayParams.z - 0.5) * _NL_RayParams.x;
                float s, c;
                sincos(angle, s, c);
                float2 axis = _NL_RayStart.zw;
                float2 dir = float2(axis.x * c - axis.y * s, axis.x * s + axis.y * c);
                float reach = _NL_RayParams.y;

                bool leaving = true;
                bool inside = false;
                float strength = 0.0;
                float exitR = reach;

                [loop]
                for (float r = 0.0; r < reach; r += _NL_RayParams.w)
                {
                    float2 p = _NL_RayStart.xy + dir * r;
                    if (p.x < 0.0 || p.y < 0.0 || p.x > _NL_OccupancySize.x || p.y > _NL_OccupancySize.y) break;

                    float2 occ = _NL_Occupancy.SampleLevel(sampler_NL_Occupancy, p * _NL_OccupancySize.zw, 0).rg;
                    bool solid = occ.x > 0.5;
                    if (leaving)
                    {
                        leaving = solid;
                        continue;
                    }
                    if (solid)
                    {
                        inside = true;
                        strength = max(strength, occ.y);
                        continue;
                    }
                    if (inside)
                    {
                        exitR = r;
                        break;
                    }
                }

                return float4(exitR / reach, inside ? strength : 0.0, 0.0, 0.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
