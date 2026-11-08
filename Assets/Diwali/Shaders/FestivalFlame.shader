Shader "Diwali/Living Flame"
{
    // A real flame is a glowing volume, so this one is too. A short ray march through a unit box
    // (x,z in -0.5..0.5, y in 0..1) gives correct parallax in each eye and holds up when viewed
    // from above, where a flat billboard collapses. Flames are small on screen, so ten steps are cheap.
    Properties
    {
        _NoiseTex("Noise", 2D) = "gray" {}
        _Intensity("Intensity", Range(0, 2)) = 1
        _Body("Body width", Range(0.1, 1.2)) = 0.52
        _Turbulence("Turbulence", Range(0, 1.5)) = 0.24
        _Speed("Rise speed", Float) = 1.25
        _Gain("Luminosity per unit of path", Float) = 2.5
        _Coverage("Coverage over the room", Range(0, 1)) = 0.95
        _Lean("Lean from motion (xz)", Vector) = (0, 0, 0, 0)
        _Seed("Seed", Float) = 0
        _Hot("Hot core", Color) = (1, 0.93, 0.6, 1)
        _Mid("Luminous body", Color) = (1, 0.64, 0.09, 1)
        _Cool("Outer edge", Color) = (1, 0.24, 0.02, 1)
        _Base("Blue base", Color) = (0.14, 0.32, 1, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" }
        Blend One OneMinusSrcAlpha
        Cull Back
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "DiwaliCommon.hlsl"

            #define FLAME_STEPS 10

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Hot, _Mid, _Cool, _Base;
                float4 _Lean;
                float _Intensity, _Body, _Turbulence, _Speed, _Gain, _Coverage, _Seed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionOS = v.positionOS.xyz;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            // Returns optical density at p and a heat value for colouring.
            float Flame(float3 p, float time, out float heat, out float radial)
            {
                float h = saturate(p.y);

                // Anchored at the wick: sway and motion lean grow towards the tip.
                float2 wander = SAMPLE_TEXTURE2D_LOD(_NoiseTex, sampler_NoiseTex, float2(_Seed + time * 0.17, h * 0.3 - time * 0.46), 0).rg - 0.5;
                float2 q = p.xz - (wander * (0.62 * _Turbulence) + _Lean.xy) * h * h;

                // Teardrop profile, widest about a third of the way up.
                float radius = _Body * 1.7 * sqrt(h) * pow(1.0 - h, 0.92);
                radial = length(q) / max(radius, 0.003);

                // Rising filaments eat into the edge, more strongly near the tip.
                float2 uv = float2(q.x * 1.6 + _Seed * 0.37, h * 0.8 - time * _Speed);
                float fine = SAMPLE_TEXTURE2D_LOD(_NoiseTex, sampler_NoiseTex, uv, 0).b - 0.5;
                float edge = 1.0 - radial + fine * _Turbulence * (0.25 + h * 1.5);

                heat = saturate(edge * 1.5);
                return smoothstep(0.0, 0.32, edge);
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float3 origin = mul(UNITY_MATRIX_I_M, float4(GetCameraPositionWS(), 1)).xyz;
                float3 direction = normalize(i.positionOS - origin);

                float3 slab0 = (float3(-0.5, 0, -0.5) - origin) / direction;
                float3 slab1 = (float3(0.5, 1, 0.5) - origin) / direction;
                float3 nearest = min(slab0, slab1);
                float3 farthest = max(slab0, slab1);
                float enter = max(max(nearest.x, nearest.y), max(nearest.z, 0.0));
                float exit = min(farthest.x, min(farthest.y, farthest.z));
                float stepSize = max(exit - enter, 0.0) / FLAME_STEPS;

                // Interleaved noise turns slice banding into imperceptible grain.
                float jitter = 0.25 + 0.5 * frac(52.9829189 * frac(dot(i.positionCS.xy, float2(0.06711056, 0.00583715))));
                float time = _Time.y;

                float luminous = 0;
                float blue = 0;

                [unroll]
                for (int s = 0; s < FLAME_STEPS; s++)
                {
                    float3 p = origin + direction * (enter + (s + jitter) * stepSize);
                    float heat, radial;
                    float density = Flame(p, time, heat, radial);

                    // Anatomy of a real flame: a thin blue skirt at the base and a nearly clear
                    // zone around the wick, where vapour has not ignited yet.
                    half skirt = smoothstep(0.16, 0.02, p.y);
                    half vapour = smoothstep(0.26, 0.05, p.y) * smoothstep(0.8, 0.1, radial);
                    float path = density * stepSize;
                    blue += path * skirt;
                    luminous += path * (1.0 - skirt) * (1.0 - 0.8 * vapour) * (0.55 + 0.45 * heat);
                }

                // Soot glows, and a flame is thin enough that its brightness is simply how much
                // of it you look through: the middle runs white, the edges fall to deep orange.
                half glow = 1.0 - exp(-luminous * _Gain);
                half3 colour = lerp(_Cool.rgb, _Mid.rgb, smoothstep(0.02, 0.5, glow));
                colour = lerp(colour, _Hot.rgb, smoothstep(0.62, 0.98, glow));
                half base = saturate(blue * _Gain * 1.3);
                half3 light = colour * glow + _Base.rgb * base * 0.75;

                half coverage = saturate(glow * 1.15) * _Coverage;
                half4 result = half4(light * _Intensity, coverage * saturate(_Intensity));
                return result;
            }
            ENDHLSL
        }
    }
}
