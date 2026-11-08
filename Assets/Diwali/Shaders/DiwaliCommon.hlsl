#ifndef DIWALI_COMMON_INCLUDED
#define DIWALI_COMMON_INCLUDED

// Shared by every festival effect. The eye buffer composites over passthrough with premultiplied
// alpha, so "rgb with alpha 0" adds light onto the real room, and "black with alpha" dims it.

float DiwaliHash(float n)
{
    return frac(sin(n * 12.9898) * 43758.5453);
}

float3 DiwaliHash3(float n)
{
    return frac(sin(float3(n, n + 1.31, n + 2.57) * float3(12.9898, 78.233, 37.719)) * 43758.5453);
}

// Temperature to colour for cooling metal sparks. 1 is white hot, 0 is a dying ember.
half3 DiwaliBlackbody(half t)
{
    half3 c;
    c.r = saturate(t * 3.4);
    c.g = saturate(t * 2.5 - 0.48);
    c.b = saturate(t * 3.2 - 2.15);
    return c * (0.35 + 0.65 * t);
}

#endif
