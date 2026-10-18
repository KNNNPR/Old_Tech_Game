Shader "PunchCard/Menu Hover Outline"
{
    Properties
    {
        [HDR] _OutlineColor("Outline Glow",Color)=(1.6,1.6,1.6,1)
        _OutlineWidth("Width in Pixels",Range(.5,6))=2
        [HideInInspector] _OutlineCentre("Mesh Centre",Vector)=(0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+50" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _OutlineColor;
            float _OutlineWidth;
            float4 _OutlineCentre;
        CBUFFER_END
        struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
        struct Varyings { float4 positionCS:SV_POSITION; };
        Varyings MaskVert(Attributes v)
        {
            Varyings o;
            o.positionCS=TransformObjectToHClip(v.positionOS.xyz);
            return o;
        }
        Varyings OutlineVert(Attributes v)
        {
            Varyings o=MaskVert(v);
            float4 centre=TransformObjectToHClip(_OutlineCentre.xyz);
            float2 radial=(o.positionCS.xy/max(o.positionCS.w,.001)-centre.xy/max(centre.w,.001))*_ScaledScreenParams.xy;
            radial*=rsqrt(max(dot(radial,radial),.0001));
            float3 normalVS=TransformWorldToViewDir(TransformObjectToWorldNormal(v.normalOS),false);
            float2 normal=mul((float2x2)UNITY_MATRIX_P,normalVS.xy)*_ScaledScreenParams.xy;
            normal*=rsqrt(max(dot(normal,normal),.0001));
            float2 direction=normal+radial*.75;
            direction*=rsqrt(max(dot(direction,direction),.0001));
            o.positionCS.xy+=direction*(_OutlineWidth*2/_ScaledScreenParams.xy)*o.positionCS.w;
            return o;
        }
        half4 Frag(Varyings i):SV_Target { return _OutlineColor; }
        ENDHLSL

        // Bit 0 is reserved for user effects in URP, including the deferred renderer.
        // Mark the entire silhouette, then reject it in the expanded pass. No white fill.
        Pass
        {
            Name "Hover Mask"
            Tags { "LightMode"="MenuHoverMask" }
            Cull Off ZWrite Off ZTest LEqual ColorMask 0
            Stencil { Ref 1 ReadMask 1 WriteMask 1 Comp Always Pass Replace }
            HLSLPROGRAM
            #pragma vertex MaskVert
            #pragma fragment Frag
            ENDHLSL
        }
        Pass
        {
            Name "Hover Contour"
            Tags { "LightMode"="MenuHoverContour" }
            Cull Off ZWrite Off ZTest LEqual
            Stencil { Ref 1 ReadMask 1 WriteMask 0 Comp NotEqual Pass Keep }
            HLSLPROGRAM
            #pragma vertex OutlineVert
            #pragma fragment Frag
            ENDHLSL
        }
    }
}
