Shader "Diwali/Surface Light"
{
    // Lies flat on a real surface. This is what ties a virtual object to the physical floor:
    //   _Shadow = 0  warm flickering light cast on the surface, added over passthrough
    //   _Shadow = 1  a soft contact shadow, which genuinely darkens the passthrough beneath
    //   _Ring   > 0  a thin annulus, used for the placement marker and blast rings
    Properties
    {
        _Tint("Tint", Color) = (1, 0.5, 0.15, 1)
        _Intensity("Intensity", Float) = 0.5
        _Shadow("Shadow mode", Range(0, 1)) = 0
        _Ring("Ring width", Range(0, 0.5)) = 0
        _RingRadius("Ring radius", Range(0, 1)) = 0.8
        _NoiseTex("Noise", 2D) = "gray" {}
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-20" "RenderType" = "Transparent" }
        Blend One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Offset -1, -1
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "DiwaliCommon.hlsl"

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _Intensity, _Shadow, _Ring, _RingRadius;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float r = length(i.uv * 2.0 - 1.0);
                half falloff = saturate(1.0 - r);
                half4 result;

                if (_Ring > 0.0)
                {
                    half line_ = smoothstep(_Ring, 0.0, abs(r - _RingRadius));
                    result = half4(_Tint.rgb * line_ * _Intensity, line_ * _Tint.a * 0.2);
                }
                else if (_Shadow > 0.5)
                {
                    half shade = falloff * falloff * (3.0 - 2.0 * falloff);
                    result = half4(0, 0, 0, shade * _Intensity);
                }
                else
                {
                    // A little noise stops the pool of light reading as a perfect gradient.
                    half mottled = 0.88 + 0.24 * SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, i.uv * 0.6 + _Time.y * 0.02).r;
                    half pool = falloff * falloff;
                    result = half4(_Tint.rgb * pool * mottled * _Intensity, 0);
                }

                return result;
            }
            ENDHLSL
        }
    }
}
