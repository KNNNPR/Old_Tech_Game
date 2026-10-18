#ifndef OLD_TECH_OMINOUS_SKY_MOTION_INCLUDED
#define OLD_TECH_OMINOUS_SKY_MOTION_INCLUDED

// Warp on the sphere so the panoramic texture's longitude seam stays continuous.
float2 OminousSkyUV(float3 direction, float time, float rotation, float drift, float distortion, float speed)
{
    float3 d = normalize(direction);
    float angle = radians(rotation + time * drift);
    float sine, cosine;
    sincos(angle, sine, cosine);
    d.xz = float2(cosine * d.x - sine * d.z, sine * d.x + cosine * d.z);
    float phase = time * speed;
    float3 warp = float3(
        sin(d.y * 7.0 + d.z * 3.0 + phase * 0.71),
        sin(d.x * 4.0 - d.z * 5.0 - phase * 0.43) * 0.32,
        sin(d.y * 6.0 - d.x * 3.0 + phase * 0.53));
    d = normalize(d + warp * distortion);
    return float2(0.5 - atan2(d.z, d.x) * INV_TWO_PI, 1.0 - acos(clamp(d.y, -1.0, 1.0)) * INV_PI);
}

half3 OminousSkySample(TEXTURE2D_PARAM(map, samplerMap), float2 uv, float mip)
{
    half3 color = SAMPLE_TEXTURE2D_LOD(map, samplerMap, uv, mip).rgb;
    // The existing painting isn't tiled; soften its two edges at the panorama join.
    half seam = (1.0h - smoothstep(0.0, 0.025, min(uv.x, 1.0 - uv.x))) * 0.5h;
    half3 oppositeEdge = SAMPLE_TEXTURE2D_LOD(map, samplerMap, float2(1.0 - uv.x, uv.y), mip).rgb;
    return lerp(color, oppositeEdge, seam);
}

#endif
