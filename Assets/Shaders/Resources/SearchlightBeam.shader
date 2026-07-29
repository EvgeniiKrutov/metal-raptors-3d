// The visible shaft of the player plane's night searchlight: a flat wedge lying in the play
// plane (the camera never rotates, so a wedge reads exactly like a cone), drawn additively so
// it glows without hiding what is behind it. The mesh is built by PlaneSearchlight.cs, which
// also scales it to whatever the beam hits and writes _Reach — how much of the light's full
// range the shaft's tip stands for — so a truncated shaft keeps the brightness it had at that
// distance instead of restarting its fade. Design notes: docs/searchlight.md.
//
// Lives in Resources so Shader.Find sees it in builds (nothing references it as an asset;
// PlaneSearchlight builds the material at runtime).
Shader "Custom/SearchlightBeam"
{
    Properties
    {
        _Color        ("Beam Color", Color)              = (1.0, 0.88, 0.62, 0.35)
        _Reach        ("Tip Reach (x range)", Float)     = 1
        _ApexOffset   ("Apex Behind Nose (x range)", Float) = 0
        _EdgeSoftness ("Edge Softness", Range(0.5, 6))   = 2.0
        _FarFade      ("Far Fade Power", Range(0.5, 4))  = 1.6
        _NoseRamp     ("Nose Ramp (x range)", Range(0, 0.5)) = 0.03
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

        Blend SrcAlpha One   // additive: the beam only ever adds light
        ZWrite Off
        Cull Off

        Pass
        {
            Name "SearchlightBeam"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Reach;
                float _ApexOffset;
                float _EdgeSoftness;
                float _FarFade;
                float _NoseRamp;
            CBUFFER_END

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;   // x = 0 at the nose -> 1 at the shaft's tip, y = -1..1 across it
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                // How far down the light's full range this fragment sits, measured from the nose:
                // the mesh's apex is buried _ApexOffset back inside the airframe, and a shaft cut
                // short by terrain never gets past its own _Reach, so it stays bright where it
                // lands.
                float along = saturate(IN.uv.x * _Reach - _ApexOffset);
                float lengthFade = saturate(1.0 - pow(along, _FarFade));

                // Nothing glows inside the plane, and the air right at the lens holds no beam
                // either, so the shaft ramps in over the first stretch past the nose.
                float noseRamp = smoothstep(0.0, _NoseRamp, along);

                // Soft sides: bright along the axis, nothing at the cone's rim.
                float lateral = saturate(1.0 - IN.uv.y * IN.uv.y);

                float alpha = _Color.a * lengthFade * noseRamp * pow(lateral, _EdgeSoftness);
                return half4(_Color.rgb, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
