#ifndef OLD_TECH_LIQUID_BLOOD_FORWARD_INCLUDED
#define OLD_TECH_LIQUID_BLOOD_FORWARD_INCLUDED

#include "LiquidBloodInput.hlsl"
#include "LiquidBloodMotion.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

struct BloodAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
    float2 lightmapUV : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct BloodVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    half3 normalWS : TEXCOORD1;
    float2 uv : TEXCOORD2;
    half fogFactor : TEXCOORD3;
    DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 4);
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

BloodVaryings BloodVertex(BloodAttributes input)
{
    BloodVaryings output = (BloodVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    VertexPositionInputs position = GetVertexPositionInputs(BloodPositionOS(input.positionOS.xyz));
    output.positionCS = position.positionCS;
    output.positionWS = position.positionWS;
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    output.fogFactor = ComputeFogFactor(position.positionCS.z);
    OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
    OUTPUT_SH(output.normalWS, output.vertexSH);
    return output;
}

half3 BloodRippleNormal(float3 positionWS, half3 normalWS, float time)
{
    // Small ripples drift downstream, giving the glossy surface a viscous flow.
    float2 p = BloodFlowPosition(positionWS, time) * _RippleScale;
    float a = dot(p, float2(0.85, 0.52)) * TWO_PI - time * _RippleSpeed;
    float b = dot(p, float2(-0.4, 0.9)) * 9.7 + time * _RippleSpeed * 0.73;
    float2 slope = (cos(a) * float2(0.85, 0.52) + cos(b) * float2(-0.4, 0.9) * 0.45) * _RippleStrength;
    half3 reference = abs(normalWS.y) < 0.95h ? half3(0, 1, 0) : half3(0, 0, 1);
    half3 tangentWS = normalize(cross(reference, normalWS));
    half3 bitangentWS = cross(normalWS, tangentWS);
    return normalize(normalWS + tangentWS * slope.x + bitangentWS * slope.y);
}

void AccumulateBloodLight(Light light, BRDFData brdf, half3 normalWS, half3 viewWS,
    half noise, inout half3 diffuse, inout half3 specular)
{
    float intensity = max(Luminance(light.color), 0.0001);
    float attenuation = light.distanceAttenuation * light.shadowAttenuation;
    half ndotl = saturate(dot(normalWS, light.direction));
    half steps = max(2.0h, floor(_LightingBands)) - 1.0h;
    half band = saturate(floor(saturate(ndotl * attenuation * intensity) * steps + 0.5h + noise * 0.15h) / steps);
    diffuse += (light.color / intensity) * band;

    // Keep the retro diffuse bands, with smooth dielectric highlights for the wet film.
    half3 radiance = min(light.color * attenuation, half3(3, 3, 3)) * ndotl;
    specular += min(DirectBRDFSpecular(brdf, normalWS, light.direction, viewWS)
        * brdf.specular * radiance * _SpecularStrength, half3(4, 4, 4));
}

half4 BloodFragment(BloodVaryings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    half4 texel = SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
    half alpha = Alpha(texel.a, _BaseColor, _Cutoff);
    half3 geometricNormalWS = normalize(input.normalWS) * IS_FRONT_VFACE(face, 1.0h, -1.0h);
    float time = BloodTime();
    half3 normalWS = BloodRippleNormal(input.positionWS, geometricNormalWS, time);
    half3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

    float2 flowPosition = BloodFlowPosition(input.positionWS, time);
    half pooling = 0.5h + 0.5h * sin(flowPosition.x * 1.3 + flowPosition.y * 1.9)
        * sin(flowPosition.y * 1.1 - flowPosition.x * 0.4);
    half3 albedo = lerp(_BaseColor.rgb, _DeepColor.rgb, pooling * _PoolVariation);
    albedo *= lerp(1.0h, 0.55h + 0.45h * saturate(texel.r * 2.0h), _TextureInfluence);
    BRDFData brdf;
    InitializeBRDFData(albedo, 0.0h, half3(0.04h, 0.04h, 0.04h), _Smoothness, alpha, brdf);

    InputData inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
    inputData.normalWS = normalWS;
    inputData.viewDirectionWS = viewWS;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
        inputData.shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
    #else
        inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
    #endif
    half4 shadowMask = SAMPLE_SHADOWMASK(input.lightmapUV);
    half noise = frac(52.9829189 * frac(dot(floor(input.positionCS.xy), float2(0.06711056, 0.00583715)))) - 0.5h;
    half3 diffuse = 0;
    half3 specular = 0;
    AccumulateBloodLight(GetMainLight(inputData.shadowCoord, input.positionWS, shadowMask), brdf, normalWS, viewWS, noise, diffuse, specular);

    #if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX)
        #if USE_CLUSTER_LIGHT_LOOP
            [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); ++lightIndex)
            {
                CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                AccumulateBloodLight(GetAdditionalLight(lightIndex, input.positionWS, shadowMask), brdf, normalWS, viewWS, noise, diffuse, specular);
            }
        #endif
        uint lightCount = GetAdditionalLightsCount();
        LIGHT_LOOP_BEGIN(lightCount)
            AccumulateBloodLight(GetAdditionalLight(lightIndex, input.positionWS, shadowMask), brdf, normalWS, viewWS, noise, diffuse, specular);
        LIGHT_LOOP_END
    #endif

    AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(inputData.normalizedScreenSpaceUV);
    half3 ambient = max(SAMPLE_GI(input.lightmapUV, input.vertexSH, normalWS) * _AmbientStrength, _ShadowFloor.xxx);
    half fresnel = Pow4(1.0h - saturate(dot(normalWS, viewWS)));
    half3 reflection = GlossyEnvironmentReflection(reflect(-viewWS, normalWS), input.positionWS,
        brdf.perceptualRoughness, ao.indirectAmbientOcclusion, inputData.normalizedScreenSpaceUV);
    reflection *= lerp(0.04h, 0.65h, fresnel) * _ReflectionStrength;
    half3 color = albedo * (ambient * ao.indirectAmbientOcclusion + diffuse * ao.directAmbientOcclusion);
    color += (specular * ao.directAmbientOcclusion + reflection) * _SpecularTint.rgb;
    return half4(MixFog(color, input.fogFactor), alpha);
}

#endif
