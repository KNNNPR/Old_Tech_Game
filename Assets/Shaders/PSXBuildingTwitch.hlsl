#ifndef OLD_TECH_PSX_BUILDING_TWITCH_INCLUDED
#define OLD_TECH_PSX_BUILDING_TWITCH_INCLUDED

float4 PSXBuildingPosition(float4 clip, float3 world)
{
    if (clip.w <= .001 || _PSXTwitch.w <= 0) return clip;
    float2 grid = max(_PSXTwitch.z, 120) * .5 * float2(_ScreenParams.x / max(_ScreenParams.y,1), 1);
    float time = _PSXPreviewTime >= 0 ? _PSXPreviewTime : _Time.y;
    float tick = floor(time * max(_PSXTwitch.y,1));
    // Matching world-space vertices share their offset, including separate window meshes.
    float3 cell = floor(world * 16 + .5);
    float seed = dot(cell,float3(12.9898,78.233,37.719));
    float2 jitter = frac(sin(seed + tick * float2(4.171,9.231) + float2(0,2.93)) * 43758.5453) - .5;
    float2 pixel = clip.xy / clip.w * grid;
    float2 snapped = floor(pixel + jitter * _PSXTwitch.x * 2 + .5);
    clip.xy = lerp(clip.xy,snapped / grid * clip.w,saturate(_PSXTwitch.w));
    return clip;
}
#endif
