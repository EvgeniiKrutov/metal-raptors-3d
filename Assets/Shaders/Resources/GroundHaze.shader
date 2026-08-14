// Valley mist: thickens the haze inside a world-height band that starts far enough away, so a
// horizon-level seam between ground and distant geometry dissolves into fog instead of drawing a
// line. Unlike RenderSettings fog this is height-aware, so a ridge's foot can be buried while its
// crest stays clear. Design notes: docs/atmospheres.md.
//
// Lives in Resources so Shader.Find sees it in builds (GroundHaze.cs builds the material at
// runtime; nothing references it as an asset).
Shader "Hidden/GroundHaze"
{
    Properties
    {
        _GroundHazeColor ("Haze Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off
        Blend SrcAlpha OneMinusSrcAlpha, Zero One

        Pass
        {
            Name "GroundHaze"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // Blit.hlsl lives in the core package from URP 17 on, not under universal/ShaderLibrary.
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float4 _GroundHazeColor;
            // x = full strength at or below this world Y, y = clear above this world Y,
            // z = peak alpha. Written by GroundHaze.cs.
            float4 _GroundHazeBand;
            // x = nothing nearer than this eye depth, y = full strength from this eye depth on.
            float4 _GroundHazeDepth;
            float4 _GroundHazeEye;
            // The view ray at viewport (0,0) and its spans across the frame, taken one unit along
            // the camera's forward axis so scaling by eye depth lands on the shaded point.
            float4 _GroundHazeRayCorner, _GroundHazeRayRight, _GroundHazeRayUp;

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv = i.texcoord;

                // The sky is already the haze colour at the horizon; fogging it would band it.
                float raw = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    if (raw <= 1e-6) return half4(0.0, 0.0, 0.0, 0.0);
                #else
                    if (raw >= 1.0 - 1e-6) return half4(0.0, 0.0, 0.0, 0.0);
                #endif

                float eye = LinearEyeDepth(raw, _ZBufferParams);
                float reach = smoothstep(_GroundHazeDepth.x, _GroundHazeDepth.y, eye);
                if (reach <= 0.0) return half4(0.0, 0.0, 0.0, 0.0);

                float3 ray = _GroundHazeRayCorner.xyz
                           + _GroundHazeRayRight.xyz * uv.x
                           + _GroundHazeRayUp.xyz * uv.y;
                float worldY = _GroundHazeEye.y + ray.y * eye;

                float band = 1.0 - smoothstep(_GroundHazeBand.x, _GroundHazeBand.y, worldY);
                return half4(_GroundHazeColor.rgb, _GroundHazeBand.z * band * reach);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
