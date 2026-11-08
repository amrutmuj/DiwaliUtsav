Shader "Diwali/MR Lit"
{
    Properties {
        [MainColor] _BaseColor("Colour",Color)=(1,1,1,1)
        [MainTexture] _BaseMap("Albedo",2D)="white"{}
        [HDR] _EmissionColor("Radiance",Color)=(0,0,0,0)
        _Metallic("Metallic",Range(0,1))=0
        _Smoothness("Smoothness",Range(0,1))=.4
    }
    SubShader {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Blend One OneMinusSrcAlpha ZWrite On
        Pass {
            Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor,_EmissionColor;float4 _BaseMap_ST;half _Metallic,_Smoothness;
            CBUFFER_END
            struct A {float4 positionOS:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;float2 uv:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A v) {V o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionWS=TransformObjectToWorld(v.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);o.normalWS=TransformObjectToWorldNormal(v.normal);o.uv=TRANSFORM_TEX(v.uv,_BaseMap);return o;}
            half3 AddLight(Light l,half3 n,half3 view,half3 albedo) {
                half diffuse=saturate(dot(n,l.direction));half spec=pow(saturate(dot(n,normalize(l.direction+view))),lerp(12,100,_Smoothness));
                return (albedo*diffuse+lerp(.04,albedo,_Metallic)*spec)*l.color*l.distanceAttenuation*l.shadowAttenuation;
            }
            half4 frag(V i):SV_Target {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half3 n=normalize(i.normalWS),albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*_BaseColor.rgb;
                half3 view=normalize(GetWorldSpaceViewDir(i.positionWS));half3 c=albedo*max(SampleSH(n),half3(.065,.065,.065))+_EmissionColor.rgb;
                c+=AddLight(GetMainLight(),n,view,albedo);
                uint count=GetAdditionalLightsCount();for(uint k=0;k<count;k++)c+=AddLight(GetAdditionalLight(k,i.positionWS),n,view,albedo);
                half4 result=half4(c,1);return result;
            }
            ENDHLSL
        }
    }
}
