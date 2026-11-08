Shader "Diwali/SoftSpark"
{
    // Particle System sparks. Added as pure light, so they brighten the room behind them rather
    // than painting over it. The hot core gives a thin, sharp streak instead of a soft blob.
    Properties
    {
        _Boost("Brightness", Float) = 2.4
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+1" "RenderType" = "Transparent" }
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

            CBUFFER_START(UnityPerMaterial)
                float _Boost;
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
                float2 d = i.uv * 2.0 - 1.0;
                float r2 = dot(d, d);
                half window = saturate(1.0 - r2);
                half core = exp(-r2 * 9.0) * window;
                half halo = exp(-r2 * 2.6) * 0.3 * window * window;
                half3 light = i.color.rgb * (core + halo) * i.color.a * _Boost;
                light += core * core * i.color.a * 0.45;
                half4 result = half4(light, 0);
                return result;
            }
            ENDHLSL
        }
    }
}
