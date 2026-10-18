Shader "Old Tech/Robot Terminal CRT"
{
    Properties
    {
        [MainTexture] _BaseMap("Apple Texture", 2D) = "black" {}
        [HDR] _PhosphorColor("Green Phosphor", Color) = (.025, 1.15, .12, 1)
        _Brightness("Brightness", Range(0, 4)) = 1.6
        _ScreenRect("Display UV Rect", Vector) = (0, .093344, 1, .813312)
        _Resolution("Terminal Resolution", Vector) = (192, 156, 0, 0)
        _IconThreshold("Apple Silhouette Threshold", Range(.001, .2)) = .012
        _IconDetail("Photographic Detail", Range(0, 1)) = 0
        _DataBrightness("Binary Stream Brightness", Range(0, 1)) = .55
        _DataSpeed("Binary Stream Speed", Range(0, .75)) = .24
        _DataStroke("Digit Stroke Width", Range(.15, .6)) = .25
        _ScanlineStrength("Scanlines", Range(0, .6)) = .22
        _FlickerStrength("Gentle Flicker", Range(0, .15)) = .035
        _GrainStrength("Phosphor Grain", Range(0, .1)) = .012
        _Curvature("Glass Curvature", Range(0, .15)) = .035
        _EdgeFade("Glass Edge Fade", Range(0, 1)) = .28
        [Header(Signal Distortion)]
        _WaveStrength("Continuous Waves", Range(0, .05)) = .01
        _WaveSpeed("Wave Speed", Range(0, 3)) = 1.1
        _RollStrength("Rolling Light Band", Range(0, .4)) = .1
        _GlowStrength("Phosphor Halo", Range(0, 1)) = .45
        _DistortionPeriod("Distortion Interval (Seconds)", Range(1, 20)) = 5
        _DistortionDuration("Distortion Duration (Seconds)", Range(.1, 1)) = .45
        _DistortionStrength("Distortion Burst", Range(0, .15)) = .065
        [HideInInspector] _PreviewTime("Preview Time (-1 = Animated)", Float) = -1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
            half4 _PhosphorColor;
            float4 _ScreenRect;
            float4 _Resolution;
            float _Brightness, _IconThreshold, _IconDetail;
            float _DataBrightness, _DataSpeed, _DataStroke, _ScanlineStrength, _FlickerStrength;
            float _GrainStrength, _Curvature, _EdgeFade, _PreviewTime;
            float _WaveStrength, _WaveSpeed, _RollStrength, _GlowStrength;
            float _DistortionPeriod, _DistortionDuration, _DistortionStrength;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
        };
        Varyings Vert(Attributes input)
        {
            UNITY_SETUP_INSTANCE_ID(input);
            Varyings output;
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            output.uv = input.uv;
            return output;
        }
        float Hash(float2 value)
        {
            float3 p = frac(float3(value.xyx) * .1031);
            p += dot(p, p.yzx + 33.33);
            return frac((p.x + p.y) * p.z);
        }
        half AppleMask(float2 screenUV)
        {
            // Preserve the FBX's original crop; waves bend the image in local display space.
            float2 textureUV = _ScreenRect.xy + saturate(screenUV) * _ScreenRect.zw;
            half3 image = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, textureUV, 0).rgb;
            half value = max(image.r, max(image.g, image.b));
            half silhouette = smoothstep(_IconThreshold * .65, _IconThreshold * 1.35, value);
            return silhouette * lerp(1.0h, saturate(value * 2.0h), _IconDetail);
        }
        float SegmentDistance(float2 samplePoint, float2 start, float2 end)
        {
            float2 segment = end - start;
            return length(samplePoint - start - segment * saturate(dot(samplePoint - start, segment) / dot(segment, segment)));
        }
        half BinaryRain(float2 screenUV, float time, float2 resolution)
        {
            const float columns = 20.0;
            const float rows = 24.0;
            float column = floor(screenUV.x * columns);
            // Each column has a stable speed and starting phase; the digits move down together.
            float speed = _DataSpeed * lerp(.55, 1.65, Hash(float2(column, 7)));
            float phase = Hash(float2(column, 19)) * 48.0;
            float2 grid = float2(screenUV.x * columns, (screenUV.y + time * speed) * rows + phase);
            float2 id = floor(grid);
            float2 p = (frac(grid) - .5) * resolution / float2(columns, rows);

            // Thin terminal glyphs, expressed in virtual display pixels.
            float2 q = abs(p) - float2(1.1, 1.85) + .45;
            float roundedBox = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - .45;
            float zero = abs(roundedBox) - _DataStroke;
            float one = min(SegmentDistance(p, float2(0, -1.85), float2(0, 1.85)),
                min(SegmentDistance(p, float2(-.95, 1.05), float2(0, 1.85)),
                    SegmentDistance(p, float2(-.95, -1.85), float2(.95, -1.85)))) - _DataStroke;
            float distance = lerp(zero, one, step(.5, Hash(id + 31.0)));
            float aa = max(fwidth(distance) * .7, .075);
            half glyph = 1.0 - smoothstep(-aa, aa, distance);

            float trail = frac((id.y + Hash(float2(column, 41)) * 32.0) / 32.0);
            float trailLength = lerp(.45, .85, Hash(float2(column, 53)));
            float fade = lerp(.25, 1.0, pow(saturate(1.0 - trail / trailLength), .65));
            float visible = (1.0 - step(trailLength, trail)) * step(.08, Hash(id + 67.0));
            return glyph * visible * fade;
        }
        float DistortionEnvelope(float time, out float cycle, out float elapsed)
        {
            float period = max(_DistortionPeriod, 1.0);
            cycle = floor(time / period);
            elapsed = time - cycle * period;
            float duration = clamp(_DistortionDuration, .05, period * .5);
            // The first burst starts after five seconds, then repeats with a soft attack and release.
            float attack = smoothstep(0.0, duration * .16, elapsed);
            float release = 1.0 - smoothstep(duration * .62, duration, elapsed);
            return step(period, time) * attack * release;
        }
        float2 SignalUV(float2 uv, float time, float burst, float cycle, float elapsed)
        {
            float2 bounded = saturate(uv);
            float anchor = sin(bounded.x * PI) * sin(bounded.y * PI);
            float phase = time * _WaveSpeed * TWO_PI;
            // Subtract the centre's displacement to keep the quiet image anchored.
            float2 wave = float2(
                sin(uv.y * TWO_PI * 3.0 - phase) - sin(PI * 3.0 - phase),
                (sin(uv.x * TWO_PI * 2.0 + phase * .7) - sin(TWO_PI + phase * .7)) * .22);
            uv += wave * _WaveStrength * anchor;

            float row = floor(bounded.y * 20.0);
            float jitter = (Hash(float2(row + cycle * 13.0, floor(time * 30.0))) - .5) * 2.0;
            float bandPosition = .12 + .76 * frac(cycle * .37 + elapsed * 1.8);
            float bandDistance = (bounded.y - bandPosition) / .065;
            float tear = exp2(-bandDistance * bandDistance * 2.0);
            float ripple = sin(bounded.y * TWO_PI * 9.0 + elapsed * 48.0);
            uv.x += (jitter * .4 + ripple * .3 + tear * .9) * _DistortionStrength * burst * anchor;
            uv.y += sin(bounded.x * TWO_PI * 3.0 + elapsed * 36.0) * _DistortionStrength * burst * .12 * anchor;
            return uv;
        }
        half4 Frag(Varyings input) : SV_Target
        {
            float time = _PreviewTime >= 0 ? _PreviewTime : _Time.y;
            float2 screenUV = (input.uv - _ScreenRect.xy) / max(_ScreenRect.zw, .0001);
            float2 centred = screenUV - .5;
            float2 curvedUV = centred * (1.0 + _Curvature * dot(centred * 2.0, centred * 2.0)) + .5;
            float cycle, elapsed;
            float burst = DistortionEnvelope(time, cycle, elapsed);
            float2 signalUV = SignalUV(curvedUV, time, burst, cycle, elapsed);
            float2 resolution = max(_Resolution.xy, 32.0);
            float2 pixelUV = (floor(signalUV * resolution) + .5) / resolution;
            half icon = AppleMask(pixelUV);
            float2 texel = 1.0 / resolution;
            half nearHalo = (AppleMask(pixelUV + float2(texel.x, 0)) + AppleMask(pixelUV - float2(texel.x, 0))
                + AppleMask(pixelUV + float2(0, texel.y)) + AppleMask(pixelUV - float2(0, texel.y))) * .25h;
            half farHalo = (AppleMask(pixelUV + float2(texel.x * 2.5, 0)) + AppleMask(pixelUV - float2(texel.x * 2.5, 0))
                + AppleMask(pixelUV + float2(0, texel.y * 2.5)) + AppleMask(pixelUV - float2(0, texel.y * 2.5))) * .25h;
            half halo = (max(0.0h, nearHalo - icon) + max(0.0h, farHalo - icon) * .35h) * _GlowStrength;
            half echo = max(0.0h, AppleMask(pixelUV + float2(_DistortionStrength * burst * .22, 0)) - icon) * burst * .32h;
            // The same streams span the whole display and continue underneath the opaque apple.
            half marks = BinaryRain(signalUV, time, resolution) * _DataBrightness * (1.0h - icon);

            // Filter the fine scanlines at a distance rather than producing moire.
            float scanVisibility = 1.0 - saturate(fwidth(screenUV.y) * resolution.y);
            half scan = 1.0h - _ScanlineStrength * scanVisibility * (.5h + .5h * cos(screenUV.y * resolution.y * TWO_PI));
            half flicker = 1.0h - _FlickerStrength * (.5h + .5h * sin(time * 47.0 + sin(time * 11.0)));
            half grain = Hash(floor(curvedUV * resolution) + floor(time * 14.0)) - .5h;
            float rollDistance = frac(screenUV.y - time * .18) - .5;
            half roll = exp2(-rollDistance * rollDistance * 450.0) * _RollStrength;
            half edgeFade = 1.0h - _EdgeFade * saturate(dot(centred * 1.45, centred * 1.45));
            float2 edge = min(curvedUV, 1.0 - curvedUV);
            half glass = smoothstep(0.0, .015, min(edge.x, edge.y));
            half signal = max(0.0h, icon + halo + echo + marks);
            half3 color = _PhosphorColor.rgb * _Brightness * signal * scan * flicker * edgeFade * (1.0h + roll + burst * .12h);
            color *= 1.0h + grain * _GrainStrength;
            return half4(color * glass, 1);
        }
        half4 DepthFrag(Varyings input) : SV_Target { return 0; }
        ENDHLSL
        Pass
        {
            Name "Terminal Phosphor"
            Tags { "LightMode"="UniversalForwardOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
    FallBack Off
}
