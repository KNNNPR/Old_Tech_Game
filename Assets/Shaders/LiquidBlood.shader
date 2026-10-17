Shader "Old Tech/Liquid Blood"
{
    Properties
    {
        [MainTexture] _BaseMap("Original Texture Detail", 2D) = "white" {}
        [MainColor] _BaseColor("Blood Color", Color) = (0.38, 0.009, 0.016, 1)
        _DeepColor("Deep Blood Color", Color) = (0.12, 0.002, 0.006, 1)
        _TextureInfluence("Original Texture Influence", Range(0, 1)) = 0.2
        _PoolVariation("Pooling Color Variation", Range(0, 1)) = 0.35
        _Smoothness("Wet Surface Smoothness", Range(0.5, 1)) = 0.92
        _SpecularStrength("Wet Highlights", Range(0, 3)) = 1.3
        _ReflectionStrength("Surface Reflections", Range(0, 2)) = 0.8
        _SpecularTint("Reflection Tint", Color) = (1, 0.92, 0.9, 1)
        _RippleStrength("Ripple Strength", Range(0, 0.15)) = 0.035
        _RippleScale("Ripple Density", Range(0.2, 8)) = 2.3
        _RippleSpeed("Ripple Speed", Range(0, 3)) = 0.65
        _FlowDirection("Flow Direction (World XZ)", Vector) = (1, 0, -0.25, 0)
        _FlowSpeed("Viscous Flow Speed", Range(0, 0.15)) = 0.025
        _SpreadStrength("Edge Spread (Metres)", Range(0, 0.08)) = 0
        _SpreadSpeed("Edge Spread Speed", Range(0, 1)) = 0.24
        _SpreadRadius("Puddle Radius (Metres)", Float) = 1.4
        [IntRange] _LightingBands("Lighting Bands", Range(2, 8)) = 4
        _AmbientStrength("Ambient Light", Range(0, 1)) = 0.35
        _ShadowFloor("Minimum Shadow Light", Range(0, 0.5)) = 0.12
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clipping", Float) = 0
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 0
        [HideInInspector] _PreviewTime("Preview Time (-1 = Animated)", Float) = -1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 200
        Cull [_Cull]
        Pass
        {
            Name "LiquidBloodForward"
            Tags { "LightMode" = "UniversalForwardOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex BloodVertex
            #pragma fragment BloodFragment
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #include "LiquidBloodForwardPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex BloodShadowVertex
            #pragma fragment ShadowPassFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "LiquidBloodInput.hlsl"
            #include "LiquidBloodMotion.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            Varyings BloodShadowVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                input.positionOS.xyz = BloodPositionOS(input.positionOS.xyz);
                return ShadowPassVertex(input);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex BloodDepthVertex
            #pragma fragment DepthOnlyFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing
            #include "LiquidBloodInput.hlsl"
            #include "LiquidBloodMotion.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            Varyings BloodDepthVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                input.position.xyz = BloodPositionOS(input.position.xyz);
                return DepthOnlyVertex(input);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex BloodDepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include "LiquidBloodInput.hlsl"
            #include "LiquidBloodMotion.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            Varyings BloodDepthNormalsVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                input.positionOS.xyz = BloodPositionOS(input.positionOS.xyz);
                return DepthNormalsVertex(input);
            }
            ENDHLSL
        }
        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex BloodMetaVertex
            #pragma fragment BloodMetaFragment
            #include "LiquidBloodInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv1 : TEXCOORD1; float2 uv2 : TEXCOORD2; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings BloodMetaVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = MetaVertexPosition(input.positionOS, input.uv1, input.uv2, unity_LightmapST, unity_DynamicLightmapST);
                return output;
            }
            half4 BloodMetaFragment(Varyings input) : SV_Target
            {
                MetaInput meta = (MetaInput)0;
                meta.Albedo = lerp(_BaseColor.rgb, _DeepColor.rgb, _PoolVariation * 0.5h);
                return MetaFragment(meta);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
