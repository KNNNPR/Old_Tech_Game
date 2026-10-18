Shader "Old Tech/Ominous Blood Sky"
{
    Properties
    {
        [MainTexture] _MainTex ("Existing panoramic sky", 2D) = "grey" {}
        [MainColor] _Tint ("Sky tint", Color) = (1,1,1,1)
        _Exposure ("Exposure", Range(0,4)) = 1
        _Rotation ("Starting rotation", Range(0,360)) = 0
        _Drift ("Drift (degrees per second)", Range(-2,2)) = 0.22
        _Distortion ("Slow cloud distortion", Range(0,0.12)) = 0.028
        _DistortionSpeed ("Distortion speed", Range(0,0.5)) = 0.09
        _CloudLayer ("Counterflowing cloud layer", Range(0,0.5)) = 0.16
        [HideInInspector] _PreviewTime ("Preview time", Float) = -1
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "RenderPipeline"="UniversalPipeline" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "OminousSkyMotion.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _Exposure, _Rotation, _Drift, _Distortion, _DistortionSpeed, _CloudLayer, _PreviewTime;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }
            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float time = _PreviewTime >= 0 ? _PreviewTime : _Time.y;
                float2 uv = OminousSkyUV(input.direction, time, _Rotation, _Drift, _Distortion, _DistortionSpeed);
                float2 underflow = OminousSkyUV(input.direction, time, _Rotation + 11, -_Drift * 0.47, _Distortion * 1.3, _DistortionSpeed * 0.67);
                half3 sky = OminousSkySample(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), uv, 0);
                half3 clouds = OminousSkySample(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), underflow, 0);
                sky = lerp(sky, sky * (0.65h + clouds * 1.4h), _CloudLayer);
                half breath = 0.96h + 0.04h * sin(time * 0.12);
                return half4(sky * _Tint.rgb * (_Exposure * breath), 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
