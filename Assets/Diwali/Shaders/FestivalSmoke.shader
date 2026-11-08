Shader "Diwali/Smoke"
{
    // Soft sprite smoke drifting in the real room. Unlike sparks it has real coverage, so it
    // veils what is behind it. Density erodes with age, so a puff thins out instead of just fading.
    Properties
    {
        _MainTex("Puff sheet", 2D) = "white" {}
        _Shade("Shadow side colour", Color) = (0.34, 0.33, 0.36, 1)
        _Thickness("Thickness", Float) = 1.7
        _Blur("Softness", Range(0, 5)) = 0
        _Cohesion("Billow continuity", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" }
        Blend One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "DiwaliCommon.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Shade;
                float _Thickness, _Blur, _Cohesion;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half4 puff = SAMPLE_TEXTURE2D_BIAS(_MainTex, sampler_MainTex, i.uv, _Blur);
                float2 cell = frac(i.uv * 4.0) * 2.0 - 1.0;
                half envelope = pow(saturate(1.0 - dot(cell, cell)), 1.5);
                puff.a = lerp(puff.a, max(puff.a, envelope * 0.72), _Cohesion);
                // Exponential, so thick smoke deepens towards its core instead of clipping to a flat disc.
                half body = 1.0 - exp(-puff.a * _Thickness);
                half density = saturate(body - (1.0 - i.color.a) * 0.55) * i.color.a;
                half3 colour = lerp(_Shade.rgb, i.color.rgb, puff.r);
                half4 result = half4(colour * density, density);
                return result;
            }
            ENDHLSL
        }
    }
}
