Shader "UI/Punch Card/Blood Curtain"
{
    Properties
    {
        [PerRendererData] _MainTex ("Blood RGBA", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Progress ("Downward spread", Range(0,1)) = 1
        _FlowTime ("Flow time", Float) = 0
        _WaveStrength ("Viscous waves", Range(0,.06)) = .026
        _FlowSpeed ("Downward flow", Range(0,.04)) = .012
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
            float _Progress, _FlowTime, _WaveStrength, _FlowSpeed;
            v2f vert(appdata v)
            {
                v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color * _Color; return o;
            }
            float hash(float x) { return frac(sin(x * 127.1 + 17.3) * 43758.5453); }
            fixed4 frag(v2f i) : SV_Target
            {
                float width = max(1, round(360 * _ScreenParams.x / _ScreenParams.y));
                float2 uv = floor(i.uv * float2(width,360)) / float2(width,360);
                float lane = floor(uv.x * 19);
                float between = smoothstep(0,1,frac(uv.x * 19));
                float frontNoise = lerp(hash(lane),hash(lane+1),between);
                float front = _Progress * 1.32 - frontNoise * .19 + sin(uv.x * 35) * .025;
                float reveal = (1 - smoothstep(front - .006, front + .009, 1 - uv.y)) * smoothstep(0,.025,_Progress);
                float t = _FlowTime;
                float wave = (sin(uv.y * 14 - t * .62 + sin(uv.y * 6 + t * .3))
                    + .32 * sin(uv.y * 36 + t * 1.15)) * _WaveStrength * _Progress;
                float otherWave = sin(uv.y * 11 + t * .43 + 2.7) * _WaveStrength * .72;
                // Different stretch, height and flow speed keep the sides asymmetric.
                float drift = min(t * _FlowSpeed,.28);
                fixed4 right = tex2D(_MainTex, saturate(float2(uv.x + wave, uv.y + drift)));
                fixed4 left = tex2D(_MainTex, saturate(float2((1 - uv.x) * .88 + .07 + otherWave, uv.y * 1.08 + drift * .63 - .16)));
                left.a *= .72;
                float alpha = 1 - (1 - right.a) * (1 - left.a);
                float3 stain = (right.rgb * right.a + left.rgb * left.a) / max(.001, right.a + left.a);
                // Thin hanging trails with round heavy tips fall at independent speeds.
                float dropLane = floor(uv.x * 18);
                float seed = hash(dropLane + 41);
                float head = frac(t * (.028 + seed * .025) + hash(dropLane + 93)) * 1.55 - .25;
                float dropX = (dropLane + .18 + .64 * seed) / 18 + sin(uv.y * 10 + t * .7 + seed * 17) * .006;
                float radius = .0025 + seed * .0035;
                float depth = 1 - uv.y;
                float tail = (1-smoothstep(radius*.45,radius,abs(uv.x-dropX)))
                    * smoothstep(head-.3,head-.035,depth) * (1-smoothstep(head,head+.005,depth));
                float2 cap = float2((uv.x-dropX) * (_ScreenParams.x/_ScreenParams.y),depth-head);
                float tip = 1-smoothstep(radius*.7,radius*1.6,length(cap));
                float drops = max(tail*.55,tip*.88);
                float combined = 1 - (1-alpha) * (1-drops);
                stain = lerp(stain * float3(.55,.42,.44),float3(.38,.004,.012),drops*.65);
                float sheen = pow(saturate(.5 + .5*sin(uv.x*27 + uv.y*13 + t*.4)),16) * .14;
                stain += float3(sheen,.015*sheen,.025*sheen) * combined;
                // Leave a dark reading area instead of covering the CRT lettering.
                float readingArea = (1-smoothstep(.30,.46,abs(uv.x-.5))) * (1-smoothstep(.15,.30,abs(uv.y-.41)));
                alpha = combined * (1 - readingArea * .84);
                float grain = .92 + .08 * hash(floor(uv.x * width) + floor(uv.y * 360) * 19);
                return fixed4(stain * grain * i.color.rgb, alpha * reveal * i.color.a);
            }
            ENDCG
        }
    }
}
