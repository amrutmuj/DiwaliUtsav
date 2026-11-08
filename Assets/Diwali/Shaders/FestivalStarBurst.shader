Shader "Diwali/Star Burst"
{
    // One aerial shell, or one small pop, in a single draw call. Every star's arc is computed in
    // the vertex shader and sampled several times along its past, so each star carries a real curved
    // trail with no per-particle CPU work.
    Properties
    {
        _ColorA("First colour", Color) = (1, 0.72, 0.28, 1)
        _ColorB("Second colour", Color) = (1, 0.25, 0.4, 1)
        _ColorC("Tail and accent colour", Color) = (1, 0.8, 0.5, 1)
        _Age("Age", Float) = 0
        _Life("Life", Float) = 2.6
        _Speed("Launch speed", Float) = 16
        _Drag("Air drag", Float) = 1.6
        _Gravity("Gravity", Float) = 2.2
        _Width("Star width", Float) = 0.1
        _Trail("Trail time per segment", Float) = 0.11
        _Pattern("Pattern", Float) = 0
        _Glitter("Late glitter", Range(0, 1)) = 0.3
        _Seed("Seed", Float) = 0
        _Boost("Brightness", Float) = 1.5
        _Stars("Stars in the mesh", Float) = 260
        _Share("Share of the stars used", Range(0, 1)) = 1
        _Mix("Share of stars burning the second colour", Range(0, 1)) = 0
        _TailShare("How far the tail turns to the third colour", Range(0, 1)) = 0
        _TailGlitter("Sparkle along the tail", Range(0, 1)) = 0
        _Pistil("Share of slow stars at the core", Range(0, 1)) = 0
        _Head("Bead at the head of each star", Float) = 0
        _FanHalf("Half angle of a fan, in radians", Float) = 1
        _FanAxis("Across a fan", Vector) = (1, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+2" "RenderType" = "Transparent" "DisableBatching" = "True" }
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

            #define TRAIL_SEGMENTS 6.0

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorA, _ColorB, _ColorC;
                float4 _FanAxis;
                float _Age, _Life, _Speed, _Drag, _Gravity, _Width, _Trail, _Pattern, _Glitter, _Seed, _Boost, _Stars;
                float _Share, _Mix, _TailShare, _TailGlitter, _Pistil, _Head, _FanHalf;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 strip : TEXCOORD0;   // x: side -1 or 1, y: trail segment 0..TRAIL_SEGMENTS
                float2 star : TEXCOORD1;    // x: star id 0..1, y: random
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 strip : TEXCOORD0;   // x: side, y: trail segment, z: this star's random number
                half4 colour : TEXCOORD1;
                float3 sprite : TEXCOORD2;  // xy: corner of the head, z: 1 on the head
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 StarDirection(float id, float random, out float speedShare)
            {
                // An even spread needs the real star count, or the stars line up in visible spirals.
                float index = id * _Stars * _Share - 0.5;
                float y = 1.0 - 2.0 * id;
                float ring = sqrt(saturate(1.0 - y * y));
                float azimuth = index * 2.39996323 + _Seed * 6.0;
                float3 sphere = float3(cos(azimuth) * ring, y, sin(azimuth) * ring);

                // A real shell is packed by hand, so no star sits exactly where the lattice says.
                sphere = normalize(sphere + (DiwaliHash3(id * 97.0 + _Seed) - 0.5) * 0.16);
                speedShare = 0.84 + 0.16 * random;

                if (_Pattern > 6.5)         // Fan: rays in one upright plane, opening towards the visitor.
                {
                    float spread = (id - 0.5) * 2.0 * _FanHalf;
                    speedShare = _FanHalf > 1.1 ? 0.8 + 0.2 * random : 0.93 + 0.07 * random;
                    return normalize(_FanAxis.xyz * sin(spread) + float3(0, cos(spread), 0));
                }
                if (_Pattern > 5.5)         // Spider: few stars on long, straight, uneven lines.
                {
                    speedShare = 0.6 + 0.4 * random;
                    return sphere;
                }
                if (_Pattern > 4.5)         // Bead: a ball filled with separate points of light.
                {
                    speedShare = pow(random, 0.42);
                    return sphere;
                }
                if (_Pattern > 3.5)         // Palm: a few thick arms of stars at staggered speeds.
                {
                    float arm = floor(random * 11.0);
                    float3 armDirection = normalize(DiwaliHash3(arm + _Seed * 3.0) * 2.0 - 1.0 + float3(0, 0.45, 0));
                    speedShare = 0.3 + 0.7 * id;
                    return normalize(armDirection + sphere * 0.05);
                }
                if (_Pattern > 2.5)         // Ring: a flat hoop, tilted differently for every shell.
                {
                    float angle = id * 6.2831853;
                    float3 u = normalize(DiwaliHash3(_Seed * 5.0) * 2.0 - 1.0);
                    float3 w = normalize(cross(u, float3(0.31, 0.87, 0.38)));
                    speedShare = 0.97 + 0.03 * random;
                    return normalize(u * cos(angle) + w * sin(angle));
                }
                if (_Pattern > 1.5)         // Willow: slow stars that hang and droop.
                {
                    speedShare = 0.5 + 0.5 * random;
                    return sphere;
                }
                if (_Pattern > 0.5)         // Chrysanthemum: an even shell with a second inner layer.
                {
                    speedShare = random > 0.75 ? 0.5 : 0.92 + 0.08 * random;
                    return sphere;
                }
                return sphere;              // Peony.
            }

            float3 Flight(float3 direction, float speed, float age)
            {
                float fade = exp(-_Drag * age);
                float3 p = direction * speed * (1.0 - fade) / _Drag;
                p.y -= _Gravity * (age / _Drag - (1.0 - fade) / (_Drag * _Drag));
                return p;
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                // A burst may use only some of its stars, spread evenly over the same sphere.
                float shown = step(v.star.x, _Share);
                float id = saturate(v.star.x / max(_Share, 0.001));
                float pistil = step(DiwaliHash(v.star.y * 37.0 + 1.3), _Pistil);

                float speedShare;
                float3 direction = StarDirection(id, v.star.y, speedShare);
                float speed = _Speed * speedShare * lerp(1.0, 0.3, pistil);
                float life = _Life * (0.72 + 0.28 * v.star.y);

                // Each star is a strip for its trail and one round sprite for its head.
                bool isHead = v.strip.y > 50.0;
                float segment = isHead ? 0.0 : v.strip.y;
                float sampleAge = max(_Age - segment * _Trail, 0.0);
                float nextAge = max(_Age - (segment + 1.0) * _Trail, 0.0);

                float3 origin = UNITY_MATRIX_M._m03_m13_m23;
                float3 here = origin + Flight(direction, speed, sampleAge);
                float3 older = origin + Flight(direction, speed, nextAge + 0.0005);

                // A star that has nearly stopped still needs length. Without it the strip folds into a thin
                // dash across its own path, and a ball of slow stars turns into swirling commas.
                float3 tip = origin + Flight(direction, speed, _Age);
                float3 heading = normalize(tip - (origin + Flight(direction, speed, max(_Age - 0.05, 0.0))) + direction * 0.0001);
                float least = _Width * 1.6 * (segment / TRAIL_SEGMENTS);
                float stretched = step(distance(tip, here), least);
                here = lerp(here, tip - heading * least, stretched);
                older = lerp(older, tip - heading * (least + _Width * 1.6 / TRAIL_SEGMENTS), stretched);

                float t = saturate(_Age / life);
                float headFade = saturate((1.0 - t) * 3.2);
                float alive = step(_Age, life);

                // Each star has its own colour, or all of them turn together as they age.
                float along = segment / TRAIL_SEGMENTS;
                float pick = DiwaliHash(v.star.y * 53.0 + 2.1);
                half colourShift = smoothstep(0.42, 0.62, t + v.star.y * 0.08) * step(_Mix, 0.0001);
                half3 star = lerp(_ColorA.rgb, _ColorB.rgb, colourShift);
                star = pick < _Mix ? _ColorB.rgb : star;
                star = pick < _Mix * 0.3 ? _ColorC.rgb : star;
                star = lerp(star, _ColorC.rgb, pistil);

                // The head is white hot. Behind it the trail cools, or turns the shell's tail colour.
                half3 tail = lerp(star * DiwaliBlackbody(0.55), _ColorC.rgb, _TailShare);
                half3 hot = lerp(star, half3(1, 0.97, 0.92), saturate(1.0 - along * 5.0) * 0.28);
                half3 light = lerp(hot, tail, smoothstep(0.22, 0.62, along));

                // Late glitter: stars flash on and off as they burn out.
                float glitterTime = floor(_Age * 21.0 + v.star.y * 13.0);
                float flicker = lerp(1.0, step(0.48, DiwaliHash(v.star.x * 91.0 + glitterTime)), _Glitter * smoothstep(0.5, 0.75, t));

                float3 eye = GetCameraPositionWS();
                float3 toEye = normalize(eye - here);
                float3 tangent = here - older;
                float tangentLength = length(tangent);
                float3 across = tangentLength > 0.00001 ? normalize(cross(tangent, toEye)) : normalize(cross(float3(0, 1, 0), toEye));

                // Never thinner than about a pixel at distance, or the burst shimmers instead of glowing.
                float pixelFloor = distance(eye, here) * 0.0011;
                float size = lerp(1.0, 0.4, pistil) * (0.55 + 0.45 * headFade);
                float width = max(_Width * (1.0 - along * 0.85) * size, pixelFloor);

                float3 world = here + across * v.strip.x * width;
                float2 corner = float2(v.strip.x, v.strip.y - 100.0);
                if (isHead)
                {
                    float radius = max(_Width * (0.6 + _Head) * size, pixelFloor * 1.6);
                    world = tip + (UNITY_MATRIX_I_V._m00_m10_m20 * corner.x + UNITY_MATRIX_I_V._m01_m11_m21 * corner.y) * radius;
                }
                world = lerp(origin, world, alive * shown);
                o.positionCS = TransformWorldToHClip(world);
                o.strip = float3(v.strip.x, segment, v.star.y);
                o.sprite = float3(corner, isHead ? 1.0 : 0.0);
                half brightness = _Boost * headFade * flicker * (1.0 - along * along) * alive * shown;

                // White here would wash a saturated star out, so the head keeps its colour and the sprite adds the core.
                o.colour = half4((isHead ? star : light) * brightness, 1);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                if (i.sprite.z > 0.5)
                {
                    half ball = saturate(1.0 - dot(i.sprite.xy, i.sprite.xy));
                    half glow = ball * ball;
                    return half4(i.colour.rgb * glow + (saturate(i.colour.rgb) * 0.7 + 0.2) * glow * glow * 0.6, 0);
                }

                half across = 1.0 - i.strip.x * i.strip.x;
                half soft = across * across;
                half along = i.strip.y / TRAIL_SEGMENTS;

                // Burning metal in the tail flickers in and out, rather than drawing one smooth line.
                half grain = DiwaliHash(floor(i.strip.y * 7.0) + i.strip.z * 91.0 + floor(_Age * 26.0));
                half sparkle = lerp(1.0, step(0.5, grain) * 1.9, _TailGlitter * smoothstep(0.08, 0.4, along));
                half core = soft * soft * soft * (1.0 - along);
                half3 light = i.colour.rgb * soft * sparkle + (saturate(i.colour.rgb) * 0.8 + 0.1) * core * 0.5;
                return half4(light, 0);
            }
            ENDHLSL
        }
    }
}
