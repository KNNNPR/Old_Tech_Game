Shader "Old Tech/Blood Sea"
{
    Properties
    {
        [MainTexture] _BaseMap ("Existing blood texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Blood tint", Color) = (0.8,0.15,0.18,1)
        _DeepColor ("Deep crimson", Color) = (0.055,0.002,0.008,1)
        _WaveHeight ("Swell height (world metres)", Range(0,2)) = 0.48
        _WaveLength ("Swell length (world metres)", Float) = 25
        _WaveSpeed ("Swell speed", Range(0,3)) = 0.65
        _RippleStrength ("Fine ripple normals", Range(0,0.5)) = 0.13
        _Flow ("Texture flow UV per second", Vector) = (0.012,0.007,0,0)
        _FlowDistortion ("Texture waviness", Range(0,0.2)) = 0.045
        _Smoothness ("Wet gloss", Range(0,1)) = 0.84
        _ReflectionStrength ("Red sky reflection", Range(0,2)) = 0.8
        _AmbientStrength ("Ambient lighting", Range(0,1)) = 0.4
        _SkyMap ("Sky reflection texture", 2D) = "black" {}
        _SkyRotation ("Sky rotation", Float) = 0
        _SkyDrift ("Sky drift", Float) = 0.22
        _SkyDistortion ("Sky distortion", Float) = 0.028
        _SkySpeed ("Sky distortion speed", Float) = 0.09
        _BeaconPosition ("Screen position (world)", Vector) = (0,0,0,0)
        _BeaconForward ("Screen facing (world)", Vector) = (1,0,0,0)
        _BeaconSize ("Screen half size", Vector) = (0.9,0.7,0,0)
        [HDR] _BeaconColor ("Screen reflection colour", Color) = (0.08,1.4,0.3,1)
        _BeaconReflection ("Screen reflection strength", Range(0,2)) = 0
        [HideInInspector] _PreviewTime ("Preview time", Float) = -1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #define _ENVIRONMENTREFLECTIONS_OFF
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "OminousSkyMotion.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_SkyMap); SAMPLER(sampler_SkyMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST, _Flow;
            half4 _BaseColor, _DeepColor;
            float _WaveHeight, _WaveLength, _WaveSpeed, _RippleStrength, _FlowDistortion;
            float _Smoothness, _ReflectionStrength, _AmbientStrength;
            float _SkyRotation, _SkyDrift, _SkyDistortion, _SkySpeed, _PreviewTime;
            float4 _BeaconPosition, _BeaconForward, _BeaconSize;
            half4 _BeaconColor;
            float _BeaconReflection;
        CBUFFER_END
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            float2 uv : TEXCOORD2;
            half fog : TEXCOORD3;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        float SeaTime() { return _PreviewTime >= 0 ? _PreviewTime : _Time.y; }
        // Three crossing swells; derivatives also drive the lighting normals.
        float3 SeaWave(float2 p, float time)
        {
            float k = TWO_PI / max(_WaveLength, 0.1);
            float a = dot(p, float2(0.94,0.342)) * k - time * _WaveSpeed;
            float b = dot(p, float2(-0.55,0.835)) * k * 2.1 - time * _WaveSpeed * 0.77;
            float c = dot(p, float2(0.18,0.984)) * k * 3.7 + time * _WaveSpeed * 1.14;
            float height = sin(a) + sin(b) * 0.34 + sin(c) * 0.13;
            float2 gradient = cos(a) * float2(0.94,0.342) * k
                + cos(b) * float2(-0.55,0.835) * (k * 2.1 * 0.34)
                + cos(c) * float2(0.18,0.984) * (k * 3.7 * 0.13);
            return float3(height, gradient) * _WaveHeight;
        }
        float3 SeaPosition(float3 positionOS)
        {
            float3 world = TransformObjectToWorld(positionOS);
            world.y += SeaWave(world.xz, SeaTime()).x;
            return world;
        }
        half3 SeaNormal(float3 world, half3 normal)
        {
            float time = SeaTime();
            float2 gradient = SeaWave(world.xz, time).yz;
            float2 p = world.xz;
            gradient += (cos(dot(p,float2(1.53,0.67)) - time * 1.4) * float2(0.8,0.35)
                + cos(dot(p,float2(-0.91,2.4)) + time * 1.03) * float2(-0.3,0.7)) * _RippleStrength;
            return normalize(normal + float3(-gradient.x,0,-gradient.y) * abs(normal.y));
        }
        Varyings SeaVertex(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input,output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionWS = SeaPosition(input.positionOS.xyz);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            output.fog = ComputeFogFactor(output.positionCS.z);
            return output;
        }
        ENDHLSL
        Pass
        {
            Name "BloodSeaForward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex SeaVertex
            #pragma fragment SeaFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            half4 SeaFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float time = SeaTime();
                float2 p = input.positionWS.xz;
                float2 warp = float2(sin(p.y * 0.19 + time * 0.21),sin(p.x * 0.16 - time * 0.17));
                float2 uv = input.uv - _Flow.xy * time + warp * _FlowDistortion;
                half3 bloodTexel = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv).rgb;
                half undercurrent = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv * 1.27 + _Flow.yx * time * 0.53 - warp * 0.02).r;
                half3 albedo = lerp(_DeepColor.rgb, bloodTexel * _BaseColor.rgb, 0.72h + undercurrent * 0.28h);
                half3 normal = SeaNormal(input.positionWS, normalize(input.normalWS));
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                InputData data = (InputData)0;
                data.positionWS = input.positionWS;
                data.normalWS = normal;
                data.viewDirectionWS = view;
                data.bakedGI = SampleSH(normal) * _AmbientStrength;
                data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                data.shadowMask = half4(1,1,1,1);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    data.shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
                #else
                    data.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.alpha = 1;
                surface.smoothness = _Smoothness;
                // Supply the moving sky reflection below, instead of URP's static sky fallback.
                surface.occlusion = 0;
                surface.normalTS = half3(0,0,1);
                half3 color = UniversalFragmentPBR(data,surface).rgb + albedo * max(data.bakedGI, half3(0.06,0.015,0.02));
                float3 reflected = reflect(-view,normal);
                float2 skyUV = OminousSkyUV(reflected,time,_SkyRotation,_SkyDrift,_SkyDistortion,_SkySpeed);
                half3 sky = OminousSkySample(TEXTURE2D_ARGS(_SkyMap,sampler_SkyMap),skyUV,2);
                half fresnel = 0.04h + 0.7h * Pow4(1 - saturate(dot(normal,view)));
                color += sky * fresnel * _ReflectionStrength;
                // Reflect the nearby emissive screen as a soft rectangle, broken up by the wave normals.
                float3 facing = normalize(_BeaconForward.xyz);
                float denominator = dot(reflected,facing);
                float hitDistance = dot(_BeaconPosition.xyz-input.positionWS,facing) / (abs(denominator)>0.001 ? denominator : 0.001);
                float3 hit = input.positionWS+reflected*max(0,hitDistance)-_BeaconPosition.xyz;
                float3 screenRight = normalize(cross(float3(0,1,0),facing));
                float2 extent = max(_BeaconSize.xy,0.01)+max(0,hitDistance)*(1-_Smoothness)*0.12;
                float2 local = float2(dot(hit,screenRight),hit.y)/extent;
                float screenMask = exp2(-2*dot(local,local))*step(0,hitDistance)*step(0.001,-denominator);
                screenMask *= exp(-max(0,hitDistance)*0.022)*(0.2+fresnel);
                color += _BeaconColor.rgb*screenMask*_BeaconReflection;
                return half4(MixFog(color,input.fog),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex SeaVertex
            #pragma fragment SeaDepth
            #pragma multi_compile_instancing
            half4 SeaDepth(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex SeaVertex
            #pragma fragment SeaDepthNormals
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 SeaDepthNormals(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half3 normal = SeaNormal(input.positionWS,normalize(input.normalWS));
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct = PackNormalOctQuadEncode(normal);
                    return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)),0);
                #else
                    return half4(normal,0);
                #endif
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex SeaShadowVertex
            #pragma fragment SeaShadowFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection, _LightPosition;
            float4 SeaShadowVertex(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 world = SeaPosition(input.positionOS.xyz);
                half3 normal = SeaNormal(world,TransformObjectToWorldNormal(input.normalOS));
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirection = normalize(_LightPosition - world);
                #else
                    float3 lightDirection = _LightDirection;
                #endif
                float4 clip = TransformWorldToHClip(ApplyShadowBias(world,normal,lightDirection));
                #if UNITY_REVERSED_Z
                    clip.z = min(clip.z,UNITY_NEAR_CLIP_VALUE);
                #else
                    clip.z = max(clip.z,UNITY_NEAR_CLIP_VALUE);
                #endif
                return clip;
            }
            half4 SeaShadowFragment() : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack Off
}
