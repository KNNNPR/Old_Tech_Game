Shader "UI/Punch Card/Blood Curtain"
{
    Properties
    {
        [PerRendererData] _MainTex ("Blood RGBA", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Progress ("Downward spread", Range(0,1)) = 1
        _FlowTime ("Flow time", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            sampler2D _MainTex;
            fixed4 _Color;
            float _Progress, _FlowTime;
            v2f vert(appdata v)
            {
                v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color * _Color; return o;
            }
            float hash(float x) { return frac(sin(x * 127.1 + 17.3) * 43758.5453); }
            fixed4 frag(v2f i) : SV_Target
            {
                float width = max(1, round(360 * _ScreenParams.x / _ScreenParams.y));
                float2 uv = floor(i.uv * float2(width,360)) / float2(width,360);
                float lane = floor(uv.x * 52);
                float front = _Progress * 1.28 - hash(lane) * .19 + sin(lane * 2.4) * .035;
                float reveal = (1 - smoothstep(front - .006, front + .009, 1 - uv.y)) * smoothstep(0,.025,_Progress);
                float wave = sin(uv.y * 17 + _FlowTime * .55) * .006 * _Progress;
                float drift = _FlowTime * .003;
                fixed4 right = tex2D(_MainTex, saturate(float2(uv.x + wave, uv.y + drift)));
                fixed4 left = tex2D(_MainTex, saturate(float2(1 - uv.x - wave, uv.y + drift * .8 - .045)));
                // Mirrored, staggered traces preserve the PNG's holes and transparent edges.
                left.a *= .84;
                float alpha = 1 - (1 - right.a) * (1 - left.a);
                float3 stain = (right.rgb * right.a + left.rgb * left.a) / max(.001, right.a + left.a);
                stain *= float3(.68,.58,.58);
                float grain = .92 + .08 * hash(floor(uv.x * width) + floor(uv.y * 360) * 19);
                return fixed4(stain * grain * i.color.rgb, alpha * reveal * i.color.a);
            }
            ENDCG
        }
    }
}
