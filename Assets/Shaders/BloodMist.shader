Shader "Old Tech/Drifting Blood Mist"
{
    Properties
    {
        _FogColor ("Mist colour", Color) = (0.38,0.2,0.21,1)
        _Density ("Density", Range(0,0.5)) = 0.11
        _Opacity ("Maximum opacity", Range(0,0.6)) = 0.3
        _Wind ("World wind", Vector) = (0.24,0,0.09,0)
        [HideInInspector] _PreviewTime ("Preview time", Float) = -1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-30" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Front ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _FogColor;
                float4 _Wind;
                float _Density, _Opacity, _PreviewTime;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 cell = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(cell),Hash(cell+float2(1,0)),f.x),lerp(Hash(cell+float2(0,1)),Hash(cell+1),f.x),f.y);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                float rawDepth = SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z
                    rawDepth = lerp(UNITY_NEAR_CLIP_VALUE,1,rawDepth);
                #endif
                float3 surface = ComputeWorldSpacePosition(uv,rawDepth,UNITY_MATRIX_I_VP);
                float3 origin = _WorldSpaceCameraPos;
                float3 direction = normalize(input.positionWS-origin);
                float surfaceDistance = max(0,dot(surface-origin,direction));
                float3 originOS = TransformWorldToObject(origin);
                float3 directionOS = mul((float3x3)unity_WorldToObject,direction);
                float3 safeDirection = lerp(-1.0,1.0,step(0.0,directionOS))*max(abs(directionOS),0.000001);
                float3 a = (-0.5-originOS)/safeDirection, b = (0.5-originOS)/safeDirection;
                float3 entry = min(a,b), exit = max(a,b);
                float begin = max(0,max(entry.x,max(entry.y,entry.z)));
                float end = min(surfaceDistance,min(exit.x,min(exit.y,exit.z)));
                if(end<=begin) discard;
                float time = _PreviewTime >= 0 ? _PreviewTime : _Time.y;
                float stepLength = (end-begin)/16;
                float depth = 0;
                float jitter = Hash(floor(input.positionCS.xy));
                [unroll] for(int i=0;i<16;i++)
                {
                    float distance = begin+(i+jitter)*stepLength;
                    float3 world = origin+direction*distance;
                    float3 local = originOS+directionOS*distance;
                    float2 p = (world.xz-_Wind.xz*time)*float2(0.044,0.19);
                    float warp = Noise(p*0.47+float2(4.1,7.2));
                    float wisps = Noise(p+float2(warp*1.8,warp*0.45));
                    wisps = smoothstep(0.43,0.76,wisps);
                    float height = smoothstep(-0.5,-0.26,local.y)*(1-smoothstep(-0.22,0.5,local.y));
                    float edge = smoothstep(0,0.07,0.5-abs(local.x))*smoothstep(0,0.07,0.5-abs(local.z));
                    depth += wisps*height*edge*stepLength*_Density;
                }
                half opacity = _Opacity*(1-exp(-depth));
                return half4(_FogColor.rgb,opacity*_FogColor.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
