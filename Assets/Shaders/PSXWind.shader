Shader "Old Tech/PSX Wind"
{
    Properties { _Kind ("0 grit, 1 paper, 2 smoke", Float) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest+20" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Kind;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; float shade : TEXCOORD1; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                // Fixed, coarse screen grid instead of smooth modern particle edges.
                float2 resolution = float2(426, 240);
                o.positionCS.xy = floor(o.positionCS.xy / o.positionCS.w * resolution * .5 + .5) / (resolution * .5) * o.positionCS.w;
                o.uv = v.uv; o.color = v.color;
                o.shade = .6 + .4 * abs(dot(TransformObjectToWorldNormal(v.normalOS), normalize(float3(.6,1,-.4))));
                return o;
            }
            float Bayer(float2 pixel)
            {
                uint2 p = (uint2)pixel & 3;
                uint value = (((p.x ^ p.y) & 1) << 3) | ((p.y & 1) << 2) | (((p.x ^ p.y) & 2) << 0) | ((p.y & 2) >> 1);
                return (value + .5) / 16.;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float2 uv = (floor(i.uv * 8) + .5) / 8;
                float alpha = i.color.a;
                float3 color = i.color.rgb;
                if (_Kind > 1.5)
                {
                    float2 p = uv * 2 - 1;
                    alpha *= saturate(1 - dot(p,p)) * 1.7;
                }
                else if (_Kind > .5)
                {
                    // A few coarse printed marks on a folded, six-vertex scrap.
                    float row = floor(uv.y * 8);
                    float ink = step(.18,uv.x) * step(uv.x,.77) * step(2,row) * step(row,5);
                    ink *= 1 - fmod(row,2);
                    color *= lerp(1,.36,ink);
                    color *= i.shade;
                }
                float2 virtualPixel = floor(i.positionCS.xy / _ScreenParams.xy * float2(426,240));
                clip(alpha - Bayer(virtualPixel));
                return half4(floor(color * 15 + .5) / 15, 1);
            }
            ENDHLSL
        }
    }
}
