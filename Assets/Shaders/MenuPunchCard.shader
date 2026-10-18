Shader "PunchCard/Menu Paper"
{
    Properties { _BaseColor("Paper", Color) = (0.72,0.63,0.43,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float2 uv:TEXCOORD1; };
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                return o;
            }
            half4 Frag(Varyings i, FRONT_FACE_TYPE facing:FRONT_FACE_SEMANTIC):SV_Target
            {
                float2 uv = i.uv;
                float2 grid = (uv - float2(0.06,0.16)) * float2(32,9);
                float2 cell = floor(grid);
                float2 p = frac(grid);
                float code = frac(sin(dot(cell,float2(13.7,71.3))) * 4375.55);
                bool body = uv.x > 0.06 && uv.x < 0.94 && uv.y > 0.16 && uv.y < 0.83;
                if (body && code > 0.68 && p.x > 0.25 && p.x < 0.7 && p.y > 0.17 && p.y < 0.78) clip(-1);
                // A clipped corner and fine printed column marks make the silhouette recognisable.
                clip(1.88 - uv.x - uv.y);
                half ink = body && p.x < 0.045 ? 0.77 : 1;
                if (uv.y > 0.88 && uv.y < 0.895 && uv.x > 0.07 && uv.x < 0.54) ink = 0.35;
                if (uv.y > 0.935 && uv.y < 0.95 && uv.x > 0.07 && uv.x < 0.28) ink = 0.48;
                if (uv.y > 0.055 && uv.y < 0.075 && frac(uv.x * 32) < 0.24) ink = 0.5;
                float3 n = normalize(i.normalWS) * (IS_FRONT_VFACE(facing,1,-1));
                Light light = GetMainLight();
                half shade = 0.38 + floor(saturate(dot(n,light.direction)) * 3) * 0.2;
                half grain = frac(sin(dot(floor(uv * float2(430,190)),float2(12.99,78.23))) * 43758.5);
                return half4(_BaseColor.rgb * shade * ink * (0.94 + grain * 0.08),1);
            }
            ENDHLSL
        }
    }
}
