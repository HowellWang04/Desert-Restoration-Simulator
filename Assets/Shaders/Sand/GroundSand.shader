Shader "Sand/GroundSand"
{
    Properties
    {
        [Header(Noise)]
        [NoScaleOffset] _NoiseTexA ("Noise Texture A", 2D) = "white" {}
        [NoScaleOffset] _NoiseTexB ("Noise Texture B", 2D) = "gray" {}
        _NoiseBlend ("Noise Blend", Range(0, 1)) = 0.5
        _Contrast ("Noise Contrast", Range(0.1, 4)) = 1.2

        [Header(Wind)]
        _WindDirection ("Wind Direction (XY)", Vector) = (1, 0, 0, 0)
        _WindSpeed ("Wind Speed", Range(0, 5)) = 1.0
        _SpeedScale ("Global Speed Scale", Float) = 0.35
        _WorldUVScale ("World UV Scale", Float) = 0.12
        [HideInInspector] _UseLocalWind ("Use Local Wind", Float) = 0

        [Header(Color Gradient)]
        _ColorDeep ("Deep Sand Color", Color) = (0.55, 0.38, 0.18, 1)
        _ColorMid ("Mid Sand Color", Color) = (0.78, 0.60, 0.32, 1)
        _ColorBright ("Bright Sand Color", Color) = (0.95, 0.82, 0.55, 1)

        [Header(Parallax Global)]
        _ParallaxAmplitude ("Parallax Amplitude", Range(0, 0.08)) = 0
        _ParallaxHeightScale ("Parallax Height Scale", Range(0, 2)) = 0

        [Header(Layer Far)]
        _FarTiling ("Far Tiling", Float) = 2.5
        _FarSpeed ("Far Speed", Float) = 0.45
        _FarParallax ("Far Parallax Depth", Range(0, 2)) = 0.25
        _FarAlpha ("Far Alpha", Range(0, 1)) = 0.55
        _FarTint ("Far Tint", Color) = (1, 1, 1, 1)

        [Header(Layer Mid)]
        _MidTiling ("Mid Tiling", Float) = 4.5
        _MidSpeed ("Mid Speed", Float) = 1.0
        _MidParallax ("Mid Parallax Depth", Range(0, 2)) = 0.55
        _MidAlpha ("Mid Alpha", Range(0, 1)) = 0.7
        _MidTint ("Mid Tint", Color) = (1, 0.98, 0.94, 1)

        [Header(Layer Near)]
        _NearTiling ("Near Tiling", Float) = 7.5
        _NearSpeed ("Near Speed", Float) = 1.8
        _NearParallax ("Near Parallax Depth", Range(0, 2)) = 0.9
        _NearAlpha ("Near Alpha", Range(0, 1)) = 0.85
        _NearTint ("Near Tint", Color) = (1, 0.95, 0.88, 1)

        [Header(Alpha)]
        _Alpha ("Base Alpha", Range(0, 1)) = 1.0
        _AlphaPower ("Alpha Power", Range(0.1, 4)) = 1.0
        _AlphaCutoff ("Alpha Soft Cutoff", Range(0, 1)) = 0.0

        // 兼容旧控制器字段（不再作为主控，但保留避免粉红材质）
        [HideInInspector] _TilingA ("Tiling A", Float) = 4.0
        [HideInInspector] _TilingB ("Tiling B", Float) = 7.0
        [HideInInspector] _SpeedA ("Speed A", Float) = 1.0
        [HideInInspector] _SpeedB ("Speed B", Float) = 0.6
        [HideInInspector] _LayerBlend ("Layer Blend", Float) = 0.5

        [Header(Render State)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "GroundSandForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite [_ZWrite]
            Cull [_Cull]
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_NoiseTexA);
            SAMPLER(sampler_NoiseTexA);
            TEXTURE2D(_NoiseTexB);
            SAMPLER(sampler_NoiseTexB);

            CBUFFER_START(UnityPerMaterial)
                float _NoiseBlend;
                float _Contrast;
                float4 _WindDirection;
                float _WindSpeed;
                float _SpeedScale;
                float _WorldUVScale;
                float _UseLocalWind;
                half4 _ColorDeep;
                half4 _ColorMid;
                half4 _ColorBright;
                float _ParallaxAmplitude;
                float _ParallaxHeightScale;
                float _FarTiling;
                float _FarSpeed;
                float _FarParallax;
                half _FarAlpha;
                half4 _FarTint;
                float _MidTiling;
                float _MidSpeed;
                float _MidParallax;
                half _MidAlpha;
                half4 _MidTint;
                float _NearTiling;
                float _NearSpeed;
                float _NearParallax;
                half _NearAlpha;
                half4 _NearTint;
                half _Alpha;
                half _AlphaPower;
                half _AlphaCutoff;
                float _TilingA;
                float _TilingB;
                float _SpeedA;
                float _SpeedB;
                float _LayerBlend;
            CBUFFER_END

            #include "GroundSandCommon.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float  fogFactor  : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 baseUV = input.positionWS.xz * _WorldUVScale;
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half4 sand = GroundSand_ComposeParallaxSand(baseUV, viewDirWS);
                sand.rgb = MixFog(sand.rgb, input.fogFactor);
                return sand;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _NoiseBlend;
                float _Contrast;
                float4 _WindDirection;
                float _WindSpeed;
                float _SpeedScale;
                float _WorldUVScale;
                float _UseLocalWind;
                half4 _ColorDeep;
                half4 _ColorMid;
                half4 _ColorBright;
                float _ParallaxAmplitude;
                float _ParallaxHeightScale;
                float _FarTiling;
                float _FarSpeed;
                float _FarParallax;
                half _FarAlpha;
                half4 _FarTint;
                float _MidTiling;
                float _MidSpeed;
                float _MidParallax;
                half _MidAlpha;
                half4 _MidTint;
                float _NearTiling;
                float _NearSpeed;
                float _NearParallax;
                half _NearAlpha;
                half4 _NearTint;
                half _Alpha;
                half _AlphaPower;
                half _AlphaCutoff;
                float _TilingA;
                float _TilingB;
                float _SpeedA;
                float _SpeedB;
                float _LayerBlend;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }
    }

    FallBack Off
}
