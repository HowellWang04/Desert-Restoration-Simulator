Shader "Sand/GroundSandTerrain"
{
    Properties
    {
        [Header(Noise)]
        [NoScaleOffset] _NoiseTexA ("Noise Texture A", 2D) = "white" {}
        [NoScaleOffset] _NoiseTexB ("Noise Texture B", 2D) = "gray" {}
        _NoiseBlend ("Noise Blend", Range(0, 1)) = 0.5
        _Contrast ("Noise Contrast", Range(0.1, 4)) = 1.25

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
        _FarAlpha ("Far Alpha", Range(0, 1)) = 0.6
        _FarTint ("Far Tint", Color) = (1, 1, 1, 1)

        [Header(Layer Mid)]
        _MidTiling ("Mid Tiling", Float) = 4.5
        _MidSpeed ("Mid Speed", Float) = 1.0
        _MidParallax ("Mid Parallax Depth", Range(0, 2)) = 0.55
        _MidAlpha ("Mid Alpha", Range(0, 1)) = 0.75
        _MidTint ("Mid Tint", Color) = (1, 0.98, 0.94, 1)

        [Header(Layer Near)]
        _NearTiling ("Near Tiling", Float) = 7.5
        _NearSpeed ("Near Speed", Float) = 1.8
        _NearParallax ("Near Parallax Depth", Range(0, 2)) = 0.9
        _NearAlpha ("Near Alpha", Range(0, 1)) = 0.9
        _NearTint ("Near Tint", Color) = (1, 0.95, 0.88, 1)

        [Header(Alpha Internal)]
        _Alpha ("Base Alpha", Range(0, 1)) = 1.0
        _AlphaPower ("Alpha Power", Range(0.1, 4)) = 1.0
        _AlphaCutoff ("Alpha Soft Cutoff", Range(0, 1)) = 0.0

        [HideInInspector] _TilingA ("Tiling A", Float) = 4.0
        [HideInInspector] _TilingB ("Tiling B", Float) = 7.0
        [HideInInspector] _SpeedA ("Speed A", Float) = 1.0
        [HideInInspector] _SpeedB ("Speed B", Float) = 0.6
        [HideInInspector] _LayerBlend ("Layer Blend", Float) = 0.5

        // ---- Terrain engine required (hidden) ----
        [HideInInspector] _Control("Control (RGBA)", 2D) = "red" {}
        [HideInInspector] _Splat3("Layer 3 (A)", 2D) = "grey" {}
        [HideInInspector] _Splat2("Layer 2 (B)", 2D) = "grey" {}
        [HideInInspector] _Splat1("Layer 1 (G)", 2D) = "grey" {}
        [HideInInspector] _Splat0("Layer 0 (R)", 2D) = "grey" {}
        [HideInInspector] _Normal3("Normal 3 (A)", 2D) = "bump" {}
        [HideInInspector] _Normal2("Normal 2 (B)", 2D) = "bump" {}
        [HideInInspector] _Normal1("Normal 1 (G)", 2D) = "bump" {}
        [HideInInspector] _Normal0("Normal 0 (R)", 2D) = "bump" {}
        [HideInInspector] _Mask3("Mask 3 (A)", 2D) = "grey" {}
        [HideInInspector] _Mask2("Mask 2 (B)", 2D) = "grey" {}
        [HideInInspector] _Mask1("Mask 1 (G)", 2D) = "grey" {}
        [HideInInspector] _Mask0("Mask 0 (R)", 2D) = "grey" {}
        [HideInInspector] _MainTex("BaseMap (RGB)", 2D) = "grey" {}
        [HideInInspector] _BaseColor("Main Color", Color) = (1,1,1,1)
        [HideInInspector] _TerrainHolesTexture("Holes Map (RGB)", 2D) = "white" {}
        [HideInInspector] [PerRendererData] _NumLayersCount ("Total Layer Count", Float) = 1.0
        [ToggleUI] _EnableInstancedPerPixelNormal("Enable Instanced per-pixel normal", Float) = 1.0
    }

    // Do not put alphatest in HLSLINCLUDE — a missing holes map would clip the whole terrain.
    SubShader
    {
        Tags
        {
            "Queue" = "Geometry-100"
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "False"
            "TerrainCompatible" = "True"
        }

        Pass
        {
            Name "ForwardSand"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap
            #pragma multi_compile_local _ _TERRAIN_INSTANCED_PERPIXEL_NORMAL

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_NoiseTexA);
            SAMPLER(sampler_NoiseTexA);
            TEXTURE2D(_NoiseTexB);
            SAMPLER(sampler_NoiseTexB);

            TEXTURE2D(_TerrainHeightmapTexture);
            TEXTURE2D(_TerrainNormalmapTexture);
            SAMPLER(sampler_TerrainNormalmapTexture);

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
                float4 _MainTex_ST;
                half4 _BaseColor;
                half _Cutoff;
            CBUFFER_END

            CBUFFER_START(_Terrain)
                float4 _Control_ST;
                float4 _Control_TexelSize;
                half _HeightTransition;
                half _NumLayersCount;
                #ifdef UNITY_INSTANCING_ENABLED
                float4 _TerrainHeightmapRecipSize;
                float4 _TerrainHeightmapScale;
                #endif
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(Terrain)
                UNITY_DEFINE_INSTANCED_PROP(float4, _TerrainPatchInstanceData)
            UNITY_INSTANCING_BUFFER_END(Terrain)

            #if defined(UNITY_INSTANCING_ENABLED) && defined(_TERRAIN_INSTANCED_PERPIXEL_NORMAL)
                #define ENABLE_TERRAIN_PERPIXEL_NORMAL
            #endif

            #include "GroundSandCommon.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 texcoord   : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uvControl  : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float  fogFactor  : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            void ApplyTerrainInstancing(inout float4 positionOS, inout float3 normalOS, inout float2 uv)
            {
            #ifdef UNITY_INSTANCING_ENABLED
                float2 patchVertex = positionOS.xy;
                float4 instanceData = UNITY_ACCESS_INSTANCED_PROP(Terrain, _TerrainPatchInstanceData);
                float2 sampleCoords = (patchVertex.xy + instanceData.xy) * instanceData.z;
                float height = UnpackHeightmap(_TerrainHeightmapTexture.Load(int3(sampleCoords, 0)));

                positionOS.xz = sampleCoords * _TerrainHeightmapScale.xz;
                positionOS.y = height * _TerrainHeightmapScale.y;

                #ifdef ENABLE_TERRAIN_PERPIXEL_NORMAL
                    normalOS = float3(0, 1, 0);
                #else
                    normalOS = _TerrainNormalmapTexture.Load(int3(sampleCoords, 0)).rgb * 2.0 - 1.0;
                #endif
                uv = sampleCoords * _TerrainHeightmapRecipSize.zw;
            #endif
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                ApplyTerrainInstancing(input.positionOS, input.normalOS, input.texcoord);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.uvControl = input.texcoord;
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            #ifdef ENABLE_TERRAIN_PERPIXEL_NORMAL
                float2 sampleCoords = (input.uvControl / _TerrainHeightmapRecipSize.zw + 0.5) * _TerrainHeightmapRecipSize.xy;
                float3 normalOS = normalize(SAMPLE_TEXTURE2D(_TerrainNormalmapTexture, sampler_TerrainNormalmapTexture, sampleCoords).rgb * 2.0 - 1.0);
                float3 normalWS = TransformObjectToWorldNormal(normalOS);
            #else
                float3 normalWS = NormalizeNormalPerPixel(input.normalWS);
            #endif

                float2 baseUV = input.positionWS.xz * max(_WorldUVScale, 1e-4);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half4 sand = GroundSand_ComposeParallaxSand(baseUV, viewDirWS);

                // Always opaque sand color as safety fallback
                half3 albedo = sand.rgb;
                if (dot(albedo, albedo) < 1e-6)
                    albedo = _ColorMid.rgb;

                Light mainLight = GetMainLight();
                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half3 lighting = mainLight.color * (ndotl * 0.55h + 0.45h) + half3(0.08h, 0.07h, 0.05h);
                albedo *= lighting;

                albedo = MixFog(albedo, input.fogFactor);
                return half4(albedo, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_TerrainHeightmapTexture);

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
                float4 _MainTex_ST;
                half4 _BaseColor;
                half _Cutoff;
            CBUFFER_END

            CBUFFER_START(_Terrain)
                float4 _Control_ST;
                float4 _Control_TexelSize;
                half _HeightTransition;
                half _NumLayersCount;
                #ifdef UNITY_INSTANCING_ENABLED
                float4 _TerrainHeightmapRecipSize;
                float4 _TerrainHeightmapScale;
                #endif
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(Terrain)
                UNITY_DEFINE_INSTANCED_PROP(float4, _TerrainPatchInstanceData)
            UNITY_INSTANCING_BUFFER_END(Terrain)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 texcoord   : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            #ifdef UNITY_INSTANCING_ENABLED
                float2 patchVertex = input.positionOS.xy;
                float4 instanceData = UNITY_ACCESS_INSTANCED_PROP(Terrain, _TerrainPatchInstanceData);
                float2 sampleCoords = (patchVertex.xy + instanceData.xy) * instanceData.z;
                float height = UnpackHeightmap(_TerrainHeightmapTexture.Load(int3(sampleCoords, 0)));
                input.positionOS.xz = sampleCoords * _TerrainHeightmapScale.xz;
                input.positionOS.y = height * _TerrainHeightmapScale.y;
            #endif

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_TerrainHeightmapTexture);

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
                float4 _MainTex_ST;
                half4 _BaseColor;
                half _Cutoff;
            CBUFFER_END

            CBUFFER_START(_Terrain)
                float4 _Control_ST;
                float4 _Control_TexelSize;
                half _HeightTransition;
                half _NumLayersCount;
                #ifdef UNITY_INSTANCING_ENABLED
                float4 _TerrainHeightmapRecipSize;
                float4 _TerrainHeightmapScale;
                #endif
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(Terrain)
                UNITY_DEFINE_INSTANCED_PROP(float4, _TerrainPatchInstanceData)
            UNITY_INSTANCING_BUFFER_END(Terrain)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 texcoord   : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            #ifdef UNITY_INSTANCING_ENABLED
                float2 patchVertex = input.positionOS.xy;
                float4 instanceData = UNITY_ACCESS_INSTANCED_PROP(Terrain, _TerrainPatchInstanceData);
                float2 sampleCoords = (patchVertex.xy + instanceData.xy) * instanceData.z;
                float height = UnpackHeightmap(_TerrainHeightmapTexture.Load(int3(sampleCoords, 0)));
                input.positionOS.xz = sampleCoords * _TerrainHeightmapScale.xz;
                input.positionOS.y = height * _TerrainHeightmapScale.y;
            #endif

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half DepthFrag(Varyings input) : SV_Target
            {
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
