Shader "Diwali/Edge Halo"
{
    // A slightly enlarged copy of an object, drawn only where the object itself is not, so what remains is
    // a soft line of light around its outline. It uses no normals, so hard corners and open meshes stay even.
    // The same shader writes the object's shape to the stencil first. Pure light, it never hides the room.
    Properties
    {
        _Tint("Tint", Color) = (1, 0.84, 0.6, 1)
        _Intensity("Intensity", Float) = 0
        _Gain("Gain per layer", Float) = 0.03
        _Spread("Share of the full width", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp("Stencil comparison", Float) = 6
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilPass("Stencil pass", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("Depth test", Float) = 4
        _ColorMask("Colour mask", Float) = 15
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+4" "RenderType" = "Transparent" }
        Blend One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest [_ZTest]
        ColorMask [_ColorMask]
        Stencil
        {
            Ref 64
            Comp [_StencilComp]
            Pass [_StencilPass]
        }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _Intensity, _Gain, _Spread;
            CBUFFER_END

            // World to world: the whole object enlarged about its middle, set per frame by EdgeHalo.
            float4x4 _Inflate;

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 world = TransformObjectToWorld(v.positionOS.xyz);
                if (_Spread > 0) world = lerp(world, mul(_Inflate, float4(world, 1)).xyz, _Spread);
                o.positionCS = TransformWorldToHClip(world);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                return half4(_Tint.rgb * (_Intensity * _Gain), 0);
            }
            ENDHLSL
        }
    }
}
