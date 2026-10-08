// The glass of a flat mirror (PlanarMirror.cs). Samples the mirror camera's texture in screen space, flipped
// left to right (that camera is a plain camera at the mirrored pose), with a faint silver tint and darker
// edges. Without a reflection yet it shows a dim silvered glass.
Shader "CastleOfDice/PlanarMirror"
{
    Properties
    {
        _ReflectionTex ("Reflection", 2D) = "black" {}
        _Tint ("Tint", Color) = (0.86, 0.9, 0.96, 1)
        _BaseColor ("Glass Without Reflection", Color) = (0.22, 0.26, 0.32, 1)
        _EdgeDarken ("Edge Darkening", Range(0, 1)) = 0.35
        _HasReflection ("Has Reflection", Float) = 0
        _Ripple ("Ripple (something stirs behind the glass)", Range(0, 1)) = 0
        _RippleColor ("Ripple Colour", Color) = (0.62, 0.3, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Mirror"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_ReflectionTex);
            SAMPLER(sampler_ReflectionTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _ReflectionTex_ST;
                half4 _Tint;
                half4 _BaseColor;
                half _EdgeDarken;
                half _HasReflection;
                half _Ripple;
                half4 _RippleColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.screenPos = ComputeScreenPos(o.positionCS);
                o.uv = input.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 s = i.screenPos.xy / i.screenPos.w;
                s.x = 1.0 - s.x;
                // Something stirs behind the glass: the reflection wavers in rings from the centre and glows violet
                float2 d = i.uv - 0.5;
                float r = length(d * float2(1.0, 0.72));
                float wave = sin(r * 38.0 - _Time.y * 7.0);
                s += d * (wave * 0.018 * _Ripple) / max(r, 0.05);
                half3 reflection = SAMPLE_TEXTURE2D(_ReflectionTex, sampler_ReflectionTex, s).rgb * _Tint.rgb;
                half3 c = lerp(_BaseColor.rgb, reflection, saturate(_HasReflection));
                c = lerp(c, _RippleColor.rgb * (0.7 + 0.3 * wave), saturate(_Ripple) * (0.35 + 0.25 * saturate(1.0 - r * 1.6)));
                float2 e = abs(i.uv - 0.5) * 2.0;
                half edge = 1.0 - _EdgeDarken * pow(saturate(max(e.x, e.y)), 6.0);
                return half4(c * edge, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
