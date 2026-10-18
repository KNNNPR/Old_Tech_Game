Shader "Old Tech/Developing Photo"
{
    Properties
    {
        [MainTexture] _Image("Photograph", 2D) = "black" {}
        _Development("Development", Range(0,1)) = 0
        _PaperColor("Paper", Color) = (.88,.86,.79,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        Pass
        {
            Name "Photo"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_Image); SAMPLER(sampler_Image);
            CBUFFER_START(UnityPerMaterial)
                float4 _Image_ST;
                half4 _PaperColor;
                float _Development;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float3 normalWS:TEXCOORD1; float3 positionWS:TEXCOORD2; float fog:TEXCOORD3; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS=TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.positionWS);
                o.normalWS=TransformObjectToWorldNormal(v.normalOS);
                // Unity's built-in Plane maps UV towards local -X/-Z; the print's right/top is +X/+Z.
                o.uv=1-v.uv;
                o.fog=ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 Frag(Varyings i, FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
            {
                float2 photoUV=(i.uv-float2(.055,.12))/float2(.89,.825);
                float inside=step(0,photoUV.x)*step(photoUV.x,1)*step(0,photoUV.y)*step(photoUV.y,1);
                half3 image=SAMPLE_TEXTURE2D(_Image,sampler_Image,saturate(photoUV)).rgb;
                half3 printColor=lerp(_PaperColor.rgb,image,inside);
                bool front=IS_FRONT_VFACE(face,true,false);
                if(!front) printColor=_PaperColor.rgb*.72;
                float grain=frac(sin(dot(floor(i.uv*256),float2(12.9898,78.233)))*43758.5453);
                float reveal=saturate(_Development+sin(_Development*3.141593)*(grain-.5)*.11);
                reveal=reveal*reveal*(3-2*reveal);
                half3 albedo=lerp(half3(.002,.002,.003),printColor,reveal);
                float3 normal=normalize(i.normalWS)*(front?1:-1);
                InputData inputData=(InputData)0;
                inputData.positionWS=i.positionWS;
                inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 shadowCoord=ComputeScreenPos(TransformWorldToHClip(i.positionWS));
                #else
                    float4 shadowCoord=TransformWorldToShadowCoord(i.positionWS);
                #endif
                Light main=GetMainLight(shadowCoord);
                half3 lighting=max(SampleSH(normal),half3(.17,.17,.17));
                lighting+=main.color*saturate(dot(normal,main.direction))*main.distanceAttenuation*main.shadowAttenuation;
                #if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX)
                    #if USE_CLUSTER_LIGHT_LOOP
                        for(uint lightIndex=0;lightIndex<min(URP_FP_DIRECTIONAL_LIGHTS_COUNT,MAX_VISIBLE_LIGHTS);lightIndex++)
                        {
                            CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                            Light light=GetAdditionalLight(lightIndex,i.positionWS);
                            lighting+=light.color*saturate(dot(normal,light.direction))*light.distanceAttenuation*light.shadowAttenuation;
                        }
                    #endif
                    uint count=GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(count)
                        Light light=GetAdditionalLight(lightIndex,i.positionWS);
                        lighting+=light.color*saturate(dot(normal,light.direction))*light.distanceAttenuation*light.shadowAttenuation;
                    LIGHT_LOOP_END
                #endif
                return half4(MixFog(albedo*lighting,i.fog),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
    FallBack "Universal Render Pipeline/Lit"
}
