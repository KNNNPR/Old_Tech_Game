Shader "OldTech/Hand Blood Spray"
{
    Properties { _Tint("Blood colour",Color)=(.65,.012,.03,1) }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct appdata {float4 vertex:POSITION;float4 colour:COLOR;float2 uv:TEXCOORD0;};
            struct v2f {float4 position:SV_POSITION;float4 colour:COLOR;float2 uv:TEXCOORD0;};
            float4 _Tint;
            v2f vert(appdata i){v2f o;o.position=TransformObjectToHClip(i.vertex.xyz);o.colour=i.colour;o.uv=i.uv;return o;}
            half4 frag(v2f i):SV_Target
            {
                float2 p=(floor(i.uv*8)+.5)/8*2-1;
                clip(.92-dot(p,p));
                float shade=saturate(.8-p.x*.2+p.y*.12);
                return half4(_Tint.rgb*i.colour.rgb*shade,_Tint.a*i.colour.a);
            }
            ENDHLSL
        }
    }
}
