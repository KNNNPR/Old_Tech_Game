Shader "UI/Punch Card/Waking Vision"
{
    Properties { _Opening("Eyes opening",Range(0,1))=0 }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"}
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
            struct v2f {float4 position:SV_POSITION;float2 uv:TEXCOORD0;};
            float _Opening;
            v2f vert(appdata i){v2f o;o.position=UnityObjectToClipPos(i.vertex);o.uv=i.uv;return o;}
            fixed4 frag(v2f i):SV_Target
            {
                float2 p=(i.uv-.5)*2;
                p.y/=.22+.78*_Opening;
                float distance=length(p*float2(1.05,1));
                float edge=lerp(-.18,1.62,_Opening);
                float alpha=smoothstep(edge-.09,edge+.12,distance);
                return fixed4(0,0,0,alpha);
            }
            ENDCG
        }
    }
}
