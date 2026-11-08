Shader "Diwali/Glow"
{
    // Camera-facing halo. Post-processing would destroy passthrough alpha, so bloom is drawn by hand:
    // this is the veil of light a bright flame leaves in the eye. Pure light, it never hides the room.
    Properties
    {
        _Tint("Tint", Color) = (1, 0.55, 0.16, 1)
        _Intensity("Intensity", Float) = 1
        _Falloff("Falloff", Float) = 5
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+5" "RenderType" = "Transparent" "DisableBatching" = "True" }
        Blend One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest Always
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "DiwaliCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _Intensity, _Falloff;
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
                float3 centre = TransformObjectToWorld(float3(0, 0, 0));
                float size = length(float3(UNITY_MATRIX_M._m00, UNITY_MATRIX_M._m10, UNITY_MATRIX_M._m20));
                float3 right = UNITY_MATRIX_I_V._m00_m10_m20;
                float3 up = UNITY_MATRIX_I_V._m01_m11_m21;
                float3 world = centre + (right * v.positionOS.x + up * v.positionOS.y) * size;
                o.positionCS = TransformWorldToHClip(world);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 d = i.uv * 2.0 - 1.0;
                float r2 = dot(d, d);
                half veil = exp(-r2 * _Falloff) * smoothstep(1.0, 0.45, r2);
                half4 result = half4(_Tint.rgb * veil * _Intensity, 0);
                return result;
            }
            ENDHLSL
        }
    }
}
