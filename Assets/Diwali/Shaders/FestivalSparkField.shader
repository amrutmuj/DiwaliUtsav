Shader "Diwali/Spark Field"
{
    // Thousands of fountain sparks at no CPU cost. A static mesh holds one quad per spark and the
    // vertex shader solves each spark's flight in closed form (drag + gravity), so a Quest can afford
    // the density a real anar has. Sparks bounce off the real floor height given in _FloorY.
    Properties
    {
        _HotColor("Hot colour", Color) = (1, 0.82, 0.42, 1)
        _CoolColor("Cooling colour", Color) = (1, 0.3, 0.05, 1)
        _SpeedMin("Speed min", Float) = 1.6
        _SpeedMax("Speed max", Float) = 5
        _HeightScale("Height scale", Float) = 1
        _Spread("Spread (radians)", Float) = 0.3
        _LifeMin("Life min", Float) = 0.5
        _LifeMax("Life max", Float) = 1.5
        _Drag("Air drag", Float) = 1.1
        _Gravity("Gravity", Float) = 5.5
        _Size("Spark width", Float) = 0.0042
        _Stretch("Streak length (seconds of motion)", Float) = 0.022
        _Radius("Emission radius", Float) = 0.02
        _Swirl("Tangential share", Range(0, 1)) = 0
        _Omega("Spin (radians per second)", Float) = 0
        _Boost("Brightness", Float) = 1.7
        [HideInInspector] _Clock("Clock", Float) = 0
        [HideInInspector] _EmitEnd("Emission end", Float) = 1000000
        [HideInInspector] _Density("Density", Float) = 1
        [HideInInspector] _FloorY("Floor height", Float) = 0
        [HideInInspector] _SpeedScale("Speed scale", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+1" "RenderType" = "Transparent" "DisableBatching" = "True" }
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
                half4 _HotColor, _CoolColor;
                float _SpeedMin, _SpeedMax, _Spread, _LifeMin, _LifeMax, _Drag, _Gravity;
                float _Size, _Stretch, _Radius, _Swirl, _Omega, _Boost, _HeightScale;
                // Per instance, written by SparkField through a property block.
                float _Clock, _EmitEnd, _Density, _FloorY, _SpeedScale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;   // Unused; the path is computed from the seed below.
                float2 corner : TEXCOORD0;      // Quad corner in -1..1
                float2 seed : TEXCOORD1;        // x: spark id in 0..1
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 corner : TEXCOORD0;
                half4 colour : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float id = v.seed.x;
                float3 rnd = DiwaliHash3(id * 61.7 + 0.13);
                float3 rnd2 = DiwaliHash3(id * 113.1 + 7.7);

                // Each spark repeats on its own cycle, staggered so emission looks continuous.
                float life = lerp(_LifeMin, _LifeMax, rnd.x * rnd.x);
                float cycle = _Clock / life + rnd.y;
                float age = frac(cycle) * life;
                float born = _Clock - age;
                float generation = floor(cycle);

                float alive = step(0.0, born) * step(born, _EmitEnd);
                alive *= step(DiwaliHash(id * 7.3 + generation * 0.73), _Density);

                // Launch direction: a cone around the object's up axis, optionally swept around a spinning rim.
                float azimuth = rnd2.x * 6.2831853 + _Omega * born;
                float3 radial = float3(cos(azimuth), 0, sin(azimuth));
                float3 tangent = float3(-radial.z, 0, radial.x);
                float tilt = sqrt(rnd2.y) * _Spread;
                float3 cone = float3(radial.x * sin(tilt), cos(tilt), radial.z * sin(tilt));
                float3 direction = normalize(lerp(cone, tangent + radial * 0.28 + float3(0, 0.1 + rnd2.z * 0.45, 0), _Swirl));
                float speed = lerp(_SpeedMin, _SpeedMax, pow(rnd.z, 1.4)) * _SpeedScale;

                float3 axisX = normalize(UNITY_MATRIX_M._m00_m10_m20);
                float3 axisY = normalize(UNITY_MATRIX_M._m01_m11_m21);
                float3 axisZ = normalize(UNITY_MATRIX_M._m02_m12_m22);
                float3 origin = UNITY_MATRIX_M._m03_m13_m23;
                float3 launch = direction.x * axisX + direction.y * axisY + direction.z * axisZ;
                float3 start = origin + (radial.x * axisX + radial.z * axisZ) * _Radius;

                // Closed-form flight with linear drag.
                float fade = exp(-_Drag * age);
                float3 position = start + launch * speed * (1.0 - fade) / _Drag;
                position.y -= _Gravity * (age / _Drag - (1.0 - fade) / (_Drag * _Drag));
                float3 velocity = launch * speed * fade;
                velocity.y -= _Gravity * (1.0 - fade) / _Drag;

                // Stretch the flight vertically about its nozzle without widening the fountain.
                position.y = start.y + (position.y - start.y) * _HeightScale;
                velocity.y *= _HeightScale;

                // A spark that reaches the real floor skips along it and dies quickly.
                float below = _FloorY - position.y;
                float landed = step(0.0, below);
                position.y = lerp(position.y, _FloorY + below * 0.22, landed);
                velocity = lerp(velocity, float3(velocity.x * 0.55, -velocity.y * 0.22, velocity.z * 0.55), landed);

                float t = age / life;
                float dying = saturate(1.0 - t * t * (1.0 + landed * 1.4));
                half heat = saturate(1.0 - t * 1.15);
                half twinkle = 0.65 + 0.35 * step(0.35, DiwaliHash(id * 3.1 + floor(_Clock * 28.0)));
                half3 light = lerp(_CoolColor.rgb * DiwaliBlackbody(0.35 + heat * 0.65), _HotColor.rgb, heat * heat);

                // Velocity-aligned streak: length is motion blur, so fast sparks read as lines.
                float3 toEye = normalize(GetCameraPositionWS() - position);
                float speedNow = length(velocity);
                float3 along = velocity / max(speedNow, 0.001);
                float3 across = normalize(cross(along, toEye));
                float halfLength = max(speedNow * _Stretch, _Size);
                float distanceToEye = distance(GetCameraPositionWS(), position);
                float width = max(_Size * (0.55 + 0.45 * rnd2.z) * dying, distanceToEye * 0.00045);

                float3 world = position + across * v.corner.x * width + along * v.corner.y * halfLength;
                world = lerp(origin, world, alive);
                o.positionCS = TransformWorldToHClip(world);
                o.corner = v.corner;
                o.colour = half4(light * twinkle * _Boost * dying, alive);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float across = 1.0 - i.corner.x * i.corner.x;
                float along = 1.0 - i.corner.y * i.corner.y;
                half shape = across * across * along;
                half4 result = half4(i.colour.rgb * shape, 0);
                return result;
            }
            ENDHLSL
        }
    }
}
