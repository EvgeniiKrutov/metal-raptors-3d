// The ground the garage's parked plane stands on: a surface that is completely invisible
// except where the main light's shadow falls on it. The garage's preview camera clears to the
// menu's background colour, so an invisible ground reads as the flat page — only the plane's
// cast shadow darkens it, which is exactly what the screen wants (a plane standing on the
// menu, not a plane sitting on a slab). Design notes: docs/garage.md.
//
// It samples the main light's shadow map directly rather than being lit, so the ground's own
// colour never depends on the light's intensity or angle — retiming the light moves the
// shadow without shifting the page colour underneath it.
//
// Lives in Resources so Shader.Find sees it in builds (nothing references it as an asset;
// GaragePlaneView builds the material at runtime).
Shader "Custom/GroundShadowCatcher"
{
    Properties
    {
        _ShadowColor ("Shadow Color", Color) = (0.0, 0.0, 0.0, 0.35)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "GroundShadowCatcher"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _ShadowColor;
            CBUFFER_END

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs positions = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = positions.positionCS;
                OUT.positionWS = positions.positionWS;
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                // Fully lit ground is fully transparent; the alpha is the shadow itself.
                half shadow = 1.0h - mainLight.shadowAttenuation;
                return half4(_ShadowColor.rgb, _ShadowColor.a * shadow);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
