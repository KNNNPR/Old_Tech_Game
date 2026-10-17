#ifndef OLD_TECH_RETRO_CEL_INPUT_INCLUDED
#define OLD_TECH_RETRO_CEL_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

// Shared by every pass so materials remain compatible with the SRP Batcher.
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    half4 _EmissionColor;
    half4 _ShadowTint;
    half _ShadeSteps;
    half _AmbientStrength;
    half _ShadowFloor;
    half _BandDither;
    half _Cutoff;
    half _Cull;
    #if defined(OLD_TECH_CRT_SCREEN)
        float4 _BaseMap_TexelSize;
        float4 _ScreenRect;
        float4 _ScrollSpeed;
        half4 _ScreenTint;
        float _ScreenEmission;
        float _WarpStrength;
        float _WarpSpeed;
        float _ScanlineDensity;
        float _ScanlineStrength;
        float _RollStrength;
        float _FlickerStrength;
        float _ScreenNoise;
        float _PreviewTime;
    #endif
CBUFFER_END

#endif
