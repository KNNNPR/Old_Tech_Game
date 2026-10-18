Shader "Old Tech/Drifting Ash"
{
    Properties { _BaseColor ("Ash colour", Color) = (0.004,0.002,0.003,0.9) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+20" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; half fog : TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.color = input.color * _BaseColor;
                output.uv = input.uv;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2 - 1;
                float edge = max(abs(p.x+0.17*p.y)*0.92+abs(p.y)*0.4, abs(p.y)*1.2);
                half alpha = (1-smoothstep(0.6,0.95,edge))*input.color.a;
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(uv),_ZBufferParams);
                float particleDepth = -TransformWorldToView(input.positionWS).z;
                alpha *= saturate((sceneDepth-particleDepth)*3);
                alpha *= 1-smoothstep(35,60,distance(input.positionWS,_WorldSpaceCameraPos));
                clip(alpha-0.01);
                return half4(MixFog(input.color.rgb,input.fog),alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
