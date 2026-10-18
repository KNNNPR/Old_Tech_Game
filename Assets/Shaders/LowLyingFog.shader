Shader "Old Tech/Low Lying Fog"
{
    Properties
    {
        _FogColor("Fog Colour",Color)=(.105,.112,.12,1)
        _Density("Density",Range(.01,2))=.35
        _TopFade("Soft Upper Edge",Range(.03,.8))=.35
        _EdgeFade("Soft Outer Edges",Range(.01,.3))=.08
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-50" }
        Pass
        {
            Name "Local Height Fog"
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Front
            ZWrite Off
            ZTest Always
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _FogColor;
                float _Density, _TopFade, _EdgeFade;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS=TransformWorldToHClip(output.positionWS);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 uv=GetNormalizedScreenSpaceUV(input.positionCS);
                float rawDepth=SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    float nearDepth=1.0;
                #else
                    float nearDepth=UNITY_NEAR_CLIP_VALUE;
                    rawDepth=lerp(UNITY_NEAR_CLIP_VALUE,1.0,rawDepth);
                #endif
                float3 surface=ComputeWorldSpacePosition(uv,rawDepth,UNITY_MATRIX_I_VP);
                float3 origin=_WorldSpaceCameraPos;
                if(unity_OrthoParams.w>0.5)
                    origin=ComputeWorldSpacePosition(uv,nearDepth,UNITY_MATRIX_I_VP);
                float3 direction=normalize(input.positionWS-origin);
                float surfaceDistance=max(0.0,dot(surface-origin,direction));
                float3 originOS=TransformWorldToObject(origin);
                float3 directionOS=mul((float3x3)unity_WorldToObject,direction);
                float3 safeDirection=lerp(-1.0,1.0,step(0.0,directionOS))*max(abs(directionOS),.000001);
                float3 a=(-.5-originOS)/safeDirection;
                float3 b=(.5-originOS)/safeDirection;
                float3 entry=min(a,b),exit=max(a,b);
                float begin=max(0.0,max(entry.x,max(entry.y,entry.z)));
                float end=min(surfaceDistance,min(exit.x,min(exit.y,exit.z)));
                if(end<=begin) discard;

                // Integrate only the part of the camera ray inside this local volume and in front of opaque geometry.
                const int steps=20;
                float stepLength=(end-begin)/steps;
                float opticalDepth=0.0;
                [unroll] for(int i=0;i<steps;i++)
                {
                    float3 p=originOS+directionOS*(begin+(i+.5)*stepLength);
                    float height=1.0-smoothstep(.5-_TopFade,.5,p.y);
                    float horizontal=smoothstep(0.0,_EdgeFade,.5-abs(p.x))*smoothstep(0.0,_EdgeFade,.5-abs(p.z));
                    opticalDepth+=height*horizontal*stepLength*_Density;
                }
                half opacity=1.0-exp(-opticalDepth);
                return half4(_FogColor.rgb,saturate(opacity*_FogColor.a));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
