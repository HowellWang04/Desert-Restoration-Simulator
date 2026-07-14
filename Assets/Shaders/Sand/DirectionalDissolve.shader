Shader "Sand/DirectionalDissolve"
{
    Properties
    {
        [Header(Base)]
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Range(0, 2)) = 1

        [Header(Sand Look)]
        _SandColor ("Sand Color", Color) = (0.78, 0.60, 0.32, 1)
        _SandMap ("Sand Map (optional)", 2D) = "white" {}
        _SandTiling ("Sand Tiling", Float) = 2.0

        [Header(Dissolve)]
        [KeywordEnum(BlowAway, Cover)] _DissolveMode ("Dissolve Mode", Float) = 0
        _DissolveNoise ("Noise Texture", 2D) = "gray" {}
        _NoiseTiling ("Noise World Tiling", Float) = 0.15
        _NoiseStrength ("Noise Strength", Range(0, 2)) = 0.55
        _DissolveStrength ("Dissolve Strength", Range(0, 1)) = 0.35
        _DissolveScale ("Directional Scale", Float) = 0.08
        _DissolveOrigin ("Dissolve Origin (XYZ)", Vector) = (0, 0, 0, 0)
        _InvertDirection ("Invert Wind Axis", Range(0, 1)) = 0

        [Header(Edge)]
        _EdgeColor ("Edge Color", Color) = (1.0, 0.72, 0.35, 1)
        _EdgeWidth ("Edge Width", Range(0.001, 0.25)) = 0.04
        _EdgeIntensity ("Edge Intensity", Range(0, 8)) = 2.5

        [Header(Wind)]
        _WindDirection ("Wind Direction (XY)", Vector) = (1, 0, 0, 0)
        _WindSpeed ("Wind Speed", Range(0, 5)) = 1
        _UseLocalWind ("Use Local Wind", Range(0, 1)) = 0
        _WindNoiseScroll ("Noise Scroll With Wind", Range(0, 1)) = 0.35

        [Header(Render)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Toggle] _AlphaClip ("Alpha Clip (BlowAway)", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma shader_feature_local _DISSOLVEMODE_BLOWAWAY _DISSOLVEMODE_COVER
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "SandWindCommon.hlsl"

            TEXTURE2D(_BaseMap);      SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);      SAMPLER(sampler_BumpMap);
            TEXTURE2D(_SandMap);      SAMPLER(sampler_SandMap);
            TEXTURE2D(_DissolveNoise); SAMPLER(sampler_DissolveNoise);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float4 _BumpMap_ST;
                half _BumpScale;
                half4 _SandColor;
                float4 _SandMap_ST;
                float _SandTiling;
                float4 _DissolveNoise_ST;
                float _NoiseTiling;
                float _NoiseStrength;
                float _DissolveStrength;
                float _DissolveScale;
                float4 _DissolveOrigin;
                float _InvertDirection;
                half4 _EdgeColor;
                float _EdgeWidth;
                float _EdgeIntensity;
                float4 _WindDirection;
                float _WindSpeed;
                float _UseLocalWind;
                float _WindNoiseScroll;
                float _AlphaClip;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float4 tangentWS  : TEXCOORD3;
                float  fogFactor  : TEXCOORD4;
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                float4 shadowCoord : TEXCOORD5;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Directional dissolve field in world space:
            // axis along wind + noise(worldXZ)
            float ComputeDissolveField(float3 positionWS)
            {
                float2 windDir = SandWind_GetDir(_WindDirection.xy, _UseLocalWind);
                if (_InvertDirection > 0.5)
                    windDir = -windDir;

                float2 centered = positionWS.xz - _DissolveOrigin.xz;
                float directional = dot(centered, windDir) * _DissolveScale;

                float windSpeed = SandWind_GetSpeed(_WindSpeed, _UseLocalWind);
                float2 noiseUV = positionWS.xz * _NoiseTiling;
                noiseUV += windDir * (_TimeParameters.x * windSpeed * _WindNoiseScroll * 0.1);

                float noise = SAMPLE_TEXTURE2D(_DissolveNoise, sampler_DissolveNoise, noiseUV).r;
                noise = (noise - 0.5) * 2.0 * _NoiseStrength;

                // Remap directional to roughly 0-1 around origin using tanh-like soft map
                float field = saturate(directional * 0.5 + 0.5 + noise * 0.5);
                return field;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = nrm.normalWS;
                real sign = input.tangentOS.w * GetOddNegativeScale();
                output.tangentWS = float4(nrm.tangentWS, sign);
                output.fogFactor = ComputeFogFactor(pos.positionCS.z);

                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                output.shadowCoord = GetShadowCoord(pos);
                #endif
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float field = ComputeDissolveField(input.positionWS);
                float threshold = _DissolveStrength;
                float edgeWidth = max(_EdgeWidth, 1e-4);
                float edge = 1.0 - saturate(abs(field - threshold) / edgeWidth);

                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                float2 sandUV = input.positionWS.xz * _SandTiling;
                half3 sandAlbedo = SAMPLE_TEXTURE2D(_SandMap, sampler_SandMap, sandUV).rgb * _SandColor.rgb;

                half3 albedo = baseSample.rgb;

                #if defined(_DISSOLVEMODE_COVER)
                    // Sand covers object: below threshold → sand, band → edge
                    float cover = 1.0 - smoothstep(threshold - edgeWidth, threshold + edgeWidth, field);
                    albedo = lerp(albedo, sandAlbedo, cover);
                    albedo = lerp(albedo, _EdgeColor.rgb, edge * cover);
                #else
                    // BlowAway: clip along wind, edge burns
                    if (_AlphaClip > 0.5)
                        clip(field - threshold);
                    albedo = lerp(albedo, _EdgeColor.rgb, edge);
                #endif

                float3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                float3x3 tbn = float3x3(input.tangentWS.xyz, bitangent, input.normalWS);
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                float3 normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, tbn));

                float4 shadowCoord = float4(0, 0, 0, 0);
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                shadowCoord = input.shadowCoord;
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif

                Light mainLight = GetMainLight(shadowCoord);
                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half3 lighting = mainLight.color * (mainLight.shadowAttenuation * ndotl * 0.7h + 0.3h);
                half3 color = albedo * lighting;
                color += _EdgeColor.rgb * edge * _EdgeIntensity;

                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma shader_feature_local _DISSOLVEMODE_BLOWAWAY _DISSOLVEMODE_COVER
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "SandWindCommon.hlsl"

            TEXTURE2D(_DissolveNoise); SAMPLER(sampler_DissolveNoise);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float4 _BumpMap_ST;
                half _BumpScale;
                half4 _SandColor;
                float4 _SandMap_ST;
                float _SandTiling;
                float4 _DissolveNoise_ST;
                float _NoiseTiling;
                float _NoiseStrength;
                float _DissolveStrength;
                float _DissolveScale;
                float4 _DissolveOrigin;
                float _InvertDirection;
                half4 _EdgeColor;
                float _EdgeWidth;
                float _EdgeIntensity;
                float4 _WindDirection;
                float _WindSpeed;
                float _UseLocalWind;
                float _WindNoiseScroll;
                float _AlphaClip;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float ComputeDissolveField(float3 positionWS)
            {
                float2 windDir = SandWind_GetDir(_WindDirection.xy, _UseLocalWind);
                if (_InvertDirection > 0.5)
                    windDir = -windDir;

                float2 centered = positionWS.xz - _DissolveOrigin.xz;
                float directional = dot(centered, windDir) * _DissolveScale;
                float windSpeed = SandWind_GetSpeed(_WindSpeed, _UseLocalWind);
                float2 noiseUV = positionWS.xz * _NoiseTiling;
                noiseUV += windDir * (_TimeParameters.x * windSpeed * _WindNoiseScroll * 0.1);
                float noise = SAMPLE_TEXTURE2D(_DissolveNoise, sampler_DissolveNoise, noiseUV).r;
                noise = (noise - 0.5) * 2.0 * _NoiseStrength;
                return saturate(directional * 0.5 + 0.5 + noise * 0.5);
            }

            Varyings ShadowVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                #if defined(_DISSOLVEMODE_COVER)
                // Covered object still casts full shadow
                #else
                if (_AlphaClip > 0.5)
                    clip(ComputeDissolveField(input.positionWS) - _DissolveStrength);
                #endif
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
