Shader "PunchCard/Menu Atmosphere"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry-10" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes i)
            {
                Varyings o; o.positionCS = TransformObjectToHClip(i.positionOS.xyz); o.uv = i.uv; return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float2 uv = i.uv;
                float2 centre = (uv-float2(.5,.66)) * float2(1.3,1.6);
                float glow = exp(-dot(centre,centre)*9);
                float edge = pow(saturate(abs(uv.x-.5)*2),8);
                float grain = frac(sin(dot(floor(uv*float2(850,480)),float2(12.9898,78.233)))*43758.54);
                half3 color = half3(.0012,.0017,.0015) + half3(.006,.0048,.0023)*glow;
                color += half3(.002,.0028,.0025)*edge;
                color *= .88 + grain*.24;
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
