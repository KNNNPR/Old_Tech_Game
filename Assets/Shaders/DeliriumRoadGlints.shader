Shader "Old Tech/Delirium Road Glints"
{
    Properties
    {
        [HDR] _Tint("Glint color",Color)=(2.2,2,1.5,1)
        _Bead("Round bead instead of star",Float)=0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend SrcAlpha One
            Cull Off ZWrite Off ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _Bead;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=v.uv;o.color=v.color;return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                // A small, crisp four-point star / pearl, with a coarse PSX texture grid.
                float2 p=(floor(saturate(i.uv)*15)+.5)/16*2-1;
                float2 a=abs(p);
                float star=saturate((1-max(a.x,a.y)-min(a.x,a.y)*4)*6);
                float bead=saturate((.55-length(p))*10);
                float shape=lerp(star,bead,step(.5,_Bead));
                clip(shape-.01);
                return half4(_Tint.rgb*i.color.rgb,shape*i.color.a*_Tint.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
