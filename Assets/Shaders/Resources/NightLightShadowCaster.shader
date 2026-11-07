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

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define NL_MAX_SHADOWS 4

            float4x4 _NL_CasterVP;
            float4 _NL_CasterLights[NL_MAX_SHADOWS];
            float4 _NL_CasterCones[NL_MAX_SHADOWS];
            float4 _NL_CasterNear;
            float _NL_CasterCount;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float objectZ : TEXCOORD1;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = mul(_NL_CasterVP, float4(o.positionWS, 1.0));
                o.objectZ = TransformObjectToWorld(float3(0.0, 0.0, 0.0)).z;
                return o;
            }

            float CasterLit(float3 positionWS, float objectZ, float4 light, float4 cone, float near)
            {
                float2 rel = positionWS.xy - light.xy;
                float along = dot(rel, cone.xy);
                float side = dot(rel, float2(-cone.y, cone.x));
                float range = cone.w;
                float edge = min(max(along + light.w, 0.0) * cone.z,
                                 sqrt(max(range * range - along * along, 0.0)));
                float extent = sqrt(max(edge * edge - side * side, 0.0));
                float depth = min(abs(positionWS.z - light.z), abs(objectZ - light.z));
                float t = depth / max(extent, 1e-3);
                float lit = (1.0 - smoothstep(0.4, 1.0, t)) * step(near, along) * step(1e-3, extent);
                return lit * step(0.02, lit);
            }

            float4 Frag(Varyings i) : SV_Target
            {
                float4 lit = 0.0;

                [unroll]
                for (int k = 0; k < NL_MAX_SHADOWS; k++)
                {
                    if (k < (int)_NL_CasterCount)
                        lit[k] = CasterLit(i.positionWS, i.objectZ, _NL_CasterLights[k], _NL_CasterCones[k], _NL_CasterNear[k]);
                }

                clip(max(max(lit.x, lit.y), max(lit.z, lit.w)) - 0.02);
                return lit;
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

            #define NL_MAX_SHADOWS 4

            Texture2D _NL_Occupancy;
            SamplerState sampler_NL_Occupancy;
            float4 _NL_OccupancySize;
            float4 _NL_RayParams;
            float4 _NL_ShadowRows[NL_MAX_SHADOWS];
            float4 _NL_ShadowSpans[NL_MAX_SHADOWS];

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
                int row = min((int)i.positionCS.y, NL_MAX_SHADOWS - 1);
                float4 start = _NL_ShadowRows[row];
                float4 span = _NL_ShadowSpans[row];
                float4 pick = (float4)(int4(0, 1, 2, 3) == row);

                float angle = (i.positionCS.x * _NL_RayParams.x - 0.5) * span.x;
                float s, c;
                sincos(angle, s, c);
                float2 axis = start.zw;
                float2 dir = float2(axis.x * c - axis.y * s, axis.x * s + axis.y * c);
                float reach = span.y;

                float2 safe = float2(abs(dir.x) > 1e-5 ? dir.x : 1e-5, abs(dir.y) > 1e-5 ? dir.y : 1e-5);
                float2 t0 = -start.xy / safe;
                float2 t1 = (_NL_OccupancySize.xy - start.xy) / safe;
                float2 lo = min(t0, t1);
                float2 hi = max(t0, t1);
                float enter = max(max(lo.x, lo.y), 0.0);
                float leave = min(min(hi.x, hi.y), reach);

                bool leaving = enter <= 0.0;
                bool inside = false;
                float segment = 0.0;
                float firstExit = reach;
                float firstStrength = 0.0;
                float strongExit = reach;
                float strongStrength = 0.0;

                [loop]
                for (float r = enter; r < leave; r += _NL_RayParams.y)
                {
                    float2 p = start.xy + dir * r;
                    float occ = dot(_NL_Occupancy.SampleLevel(sampler_NL_Occupancy, p * _NL_OccupancySize.zw, 0), pick);
                    bool solid = occ > 0.01;
                    if (leaving)
                    {
                        leaving = solid;
                        continue;
                    }
                    if (solid)
                    {
                        inside = true;
                        segment = max(segment, occ);
                        continue;
                    }
                    if (!inside) continue;

                    inside = false;
                    if (firstStrength <= 0.0)
                    {
                        firstExit = r;
                        firstStrength = segment;
                    }
                    else if (segment > max(firstStrength, strongStrength))
                    {
                        strongExit = r;
                        strongStrength = segment;
                    }
                    segment = 0.0;
                    if (max(firstStrength, strongStrength) >= 0.999) break;
                }

                return float4(firstExit / reach, firstStrength, strongExit / reach, strongStrength);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
