#ifndef OLD_TECH_LIQUID_BLOOD_INPUT_INCLUDED
#define OLD_TECH_LIQUID_BLOOD_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    half4 _DeepColor;
    half4 _SpecularTint;
    half _TextureInfluence;
    half _PoolVariation;
    half _Smoothness;
    half _SpecularStrength;
    half _ReflectionStrength;
    half _RippleStrength;
    half _RippleScale;
    half _RippleSpeed;
    float4 _FlowDirection;
    half _FlowSpeed;
    half _SpreadStrength;
    half _SpreadSpeed;
    half _SpreadRadius;
    half _LightingBands;
    half _AmbientStrength;
    half _ShadowFloor;
    half _Cutoff;
    half _Cull;
    float _PreviewTime;
CBUFFER_END

#endif
