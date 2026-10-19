Shader "OldTech/Oven Fire"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float seed : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = p.positionCS;
                output.uv = input.uv; output.color = input.color;
                output.seed = dot(p.positionWS.xz, float2(2.1, 3.7));
                return output;
            }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 cell = floor(p), f = frac(p); f = f*f*(3-2*f);
                return lerp(lerp(Hash(cell),Hash(cell+float2(1,0)),f.x),lerp(Hash(cell+float2(0,1)),Hash(cell+1),f.x),f.y);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float t = _Time.y * 5;
                float turbulence = Noise(float2(uv.x*5 + input.seed, uv.y*7-t));
                float bend = sin(uv.y*8 - t + input.seed) * .12 * uv.y;
                float x = abs(uv.x-.5 + bend);
                float width = .43 * pow(saturate(1-uv.y), .65) + .02;
                float edge = smoothstep(0, .12, width-x + (turbulence-.5)*.16);
                float alpha = edge * smoothstep(0,.12,uv.y) * smoothstep(0,.1,1-uv.y);
                float hot = saturate((1-uv.y)*.9 + (.2-x)*1.8 + turbulence*.15);
                float3 color = lerp(float3(1.4,.025,.001),float3(3.1,1.35,.12),hot);
                return half4(color * input.color.rgb, alpha * input.color.a);
            }
            ENDHLSL
        }
    }
}
