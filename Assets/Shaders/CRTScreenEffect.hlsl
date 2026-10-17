#ifndef OLD_TECH_CRT_SCREEN_EFFECT_INCLUDED
#define OLD_TECH_CRT_SCREEN_EFFECT_INCLUDED

float CRTNoise(float2 pixel, float frame)
{
    float3 p = frac(float3(pixel, frame) * 0.1031);
    p += dot(p, p.yzx + 33.33);
    return frac((p.x + p.y) * p.z);
}

half3 CRTScreenColor(float2 atlasUV, out half mask)
{
    float2 size = max(_ScreenRect.zw, float2(0.0001, 0.0001));
    float2 displayUV = (atlasUV - _ScreenRect.xy) / size;
    float2 edge = min(displayUV, 1.0 - displayUV);
    mask = smoothstep(0.0, 0.025, min(edge.x, edge.y));

    float time = _PreviewTime >= 0.0 ? _PreviewTime : _Time.y;
    float wave = sin(displayUV.y * 27.0 + time * _WarpSpeed * 3.0);
    wave += 0.35 * sin(displayUV.y * 71.0 - time * _WarpSpeed * 1.7);
    float2 movingUV = frac(displayUV + _ScrollSpeed.xy * time + float2(wave * _WarpStrength, 0));

    // Repeat inside the display's atlas rectangle, never across neighbouring panels.
    // Keep a one-texel guard and use mip 0 to prevent atlas bleed into the CRT image.
    float2 inset = min(_BaseMap_TexelSize.xy, size * 0.1);
    float2 sampleUV = _ScreenRect.xy + inset + movingUV * (size - inset * 2.0);
    half3 image = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, sampleUV, 0).rgb;
    half scanline = 1.0h - _ScanlineStrength * (0.5h + 0.5h * sin(displayUV.y * _ScanlineDensity * TWO_PI));
    float rollDistance = frac(displayUV.y - time * 0.16) - 0.5;
    half roll = exp2(-rollDistance * rollDistance * 500.0) * _RollStrength;
    half flicker = 1.0h - _FlickerStrength * (0.5h + 0.5h * sin(time * 43.0 + sin(time * 17.0)));
    half noise = CRTNoise(floor(displayUV * float2(256, 192)), floor(time * 12.0)) - 0.5h;
    half3 phosphor = max(0.0h, image * scanline * (1.0h + roll) * flicker + noise * _ScreenNoise);

    // HDR emission feeds Bloom before the existing retro palette/dither pass.
    return phosphor * _BaseColor.rgb * _ScreenTint.rgb * _ScreenEmission;
}

#endif
