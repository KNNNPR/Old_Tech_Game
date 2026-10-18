Shader "PunchCard/Menu Computer Screen"
{
    Properties
    {
        _Power("Power",Range(0,1))=1
        [HDR] _Phosphor("Phosphor",Color)=(.13,.45,.27,1)
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
        Pass
        {
            Tags {"LightMode"="UniversalForwardOnly"}
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
            CBUFFER_START(UnityPerMaterial)
            float _Power;half4 _Phosphor;
            CBUFFER_END
            Varyings Vert(Attributes v){Varyings o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=v.uv;return o;}
            half4 Frag(Varyings i):SV_Target
            {
                float2 uv=(i.uv-float2(.03125,.046875))/float2(.4609375,.421875);
                float grain=frac(sin(dot(floor(uv*float2(360,280)),float2(12.9898,78.233))+floor(_Time.y*12))*43758.54);
                float scan=.8+.2*sin(uv.y*210*6.2831853);
                float vignette=saturate(1-dot((uv-.5)*1.25,(uv-.5)*1.25));
                float roll=exp2(-pow(frac(uv.y-_Time.y*.12)-.5,2)*600)*.025;
                half3 color=_Phosphor.rgb*(.05*scan*vignette+grain*.006+roll);
                return half4(lerp(half3(.001,.002,.0015),color,_Power),1);
            }
            ENDHLSL
        }
    }
}
