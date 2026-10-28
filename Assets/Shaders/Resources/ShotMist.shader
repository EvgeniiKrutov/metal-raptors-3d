Shader "Hidden/ShotMist"
{
    Properties
    {
        _ShotMistColor ("Mist Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off
        Blend SrcAlpha OneMinusSrcAlpha, Zero One

        Pass
        {
            Name "ShotMist"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_ShotMistGround);
            SAMPLER(sampler_ShotMistGround);

            float4 _ShotMistColor;
            float4 _ShotMistBand;
            float4 _ShotMistDepth;
            float4 _ShotMistPatch;
            float4 _ShotMistGroundRect;
            float4 _ShotMistEye;
            float4 _ShotMistRayCorner, _ShotMistRayRight, _ShotMistRayUp;

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = lerp(Hash(i), Hash(i + float2(1.0, 0.0)), u.x);
                float b = lerp(Hash(i + float2(0.0, 1.0)), Hash(i + float2(1.0, 1.0)), u.x);
                return lerp(a, b, u.y);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv = i.texcoord;

                float raw = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    if (raw <= 1e-6) return half4(0.0, 0.0, 0.0, 0.0);
                #else
                    if (raw >= 1.0 - 1e-6) return half4(0.0, 0.0, 0.0, 0.0);
                #endif

                float eye = LinearEyeDepth(raw, _ZBufferParams);
                float3 ray = _ShotMistRayCorner.xyz
                           + _ShotMistRayRight.xyz * uv.x
                           + _ShotMistRayUp.xyz * uv.y;
                float3 world = _ShotMistEye.xyz + ray * eye;

                float2 groundUv = saturate((world.xz - _ShotMistGroundRect.xy) / _ShotMistGroundRect.zw);
                float ground = SAMPLE_TEXTURE2D_LOD(_ShotMistGround, sampler_ShotMistGround, groundUv, 0).r;
                float band = 1.0 - smoothstep(_ShotMistBand.x, _ShotMistBand.y, world.y - ground);
                if (band <= 0.0) return half4(0.0, 0.0, 0.0, 0.0);

                float reach = lerp(_ShotMistDepth.z, 1.0, smoothstep(_ShotMistDepth.x, _ShotMistDepth.y, eye));

                float2 p = world.xz * _ShotMistPatch.x + _ShotMistPatch.zw;
                float n = Noise(p) * 0.6 + Noise(p * 2.3 + 17.0) * 0.3 + Noise(p * 5.1 + 41.0) * 0.1;
                float patch = lerp(1.0, smoothstep(0.3, 0.7, n), _ShotMistPatch.y);

                return half4(_ShotMistColor.rgb, saturate(_ShotMistBand.z * band * reach * patch));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
