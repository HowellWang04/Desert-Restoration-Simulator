Shader "Sand/AirborneSand"
{
    Properties
    {
        [Header(Noise)]
        [NoScaleOffset] _NoiseTexA ("Noise Texture A", 2D) = "white" {}
        [NoScaleOffset] _NoiseTexB ("Noise Texture B", 2D) = "gray" {}
        _NoiseBlend ("Noise Blend", Range(0, 1)) = 0.45
        _Contrast ("Contrast", Range(0.2, 4)) = 1.4
        _Tiling ("Tiling", Float) = 0.045
        _DetailTiling ("Detail Tiling Scale", Float) = 2.3

        [Header(Flow)]
        _SpeedScale ("Speed Scale", Float) = 0.55
        _LayerSpeed ("Layer Speed", Float) = 1.0
        _VerticalDrift ("Vertical Drift", Range(0, 0.2)) = 0.02

        [Header(Color)]
        _ColorNear ("Near Color", Color) = (0.82, 0.68, 0.42, 1)
        _ColorFar ("Far Color", Color) = (0.62, 0.48, 0.30, 1)
        _Density ("Density", Range(0, 2)) = 0.55
        _AlphaPower ("Alpha Power", Range(0.3, 4)) = 1.35

        [Header(Soft Fade)]
        _SoftDepthFade ("Soft Depth Fade", Range(0.05, 8)) = 1.8
        _NearFade ("Camera Near Fade", Range(0.1, 20)) = 2.5
        _FarFadeStart ("Camera Far Fade Start", Float) = 40
        _FarFadeEnd ("Camera Far Fade End", Float) = 90
        _EdgeFade ("Sheet Edge Fade", Range(0.01, 0.5)) = 0.18
        _HeightFade ("Height Density Scale", Range(0.2, 2)) = 1.0

        [Header(Render)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
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
            Name "AirborneSandForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_NoiseTexA);
            SAMPLER(sampler_NoiseTexA);
            TEXTURE2D(_NoiseTexB);
            SAMPLER(sampler_NoiseTexB);

            // From WindManager (globals)
            float4 _SandFlowUV;
            float4 _SandGlobalWind;

            CBUFFER_START(UnityPerMaterial)
                float _NoiseBlend;
                float _Contrast;
                float _Tiling;
                float _DetailTiling;
                float _SpeedScale;
                float _LayerSpeed;
                float _VerticalDrift;
                half4 _ColorNear;
                half4 _ColorFar;
                half _Density;
                half _AlphaPower;
                float _SoftDepthFade;
                float _NearFade;
                float _FarFadeStart;
                float _FarFadeEnd;
                float _EdgeFade;
                float _HeightFade;
            CBUFFER_END

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
                float4 screenPos  : TEXCOORD2;
                float  fogFactor  : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float2 GetWindDir()
            {
                float2 dir = _SandGlobalWind.w > 0.5 ? _SandGlobalWind.xy : float2(1.0, 0.0);
                float lenSq = dot(dir, dir);
                return lenSq > 1e-6 ? dir * rsqrt(lenSq) : float2(1.0, 0.0);
            }

            float GetWindSpeed()
            {
                return _SandGlobalWind.w > 0.5 ? max(_SandGlobalWind.z, 0.0) : 1.0;
            }

            float2 GetFlowScroll()
            {
                // GPU continuous time — smooth even when CPU frames hitch
                return GetWindDir() * (_TimeParameters.x * GetWindSpeed() * _LayerSpeed * _SpeedScale);
            }

            // Procedural noise removes texture-wrap seams that look like "pause then continue"
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float Fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    v += ValueNoise(p) * a;
                    p = p * 2.03 + float2(17.1, 9.7);
                    a *= 0.5;
                }
                return v;
            }

            half RemapContrast(half n)
            {
                return saturate((n - 0.5h) * _Contrast + 0.5h);
            }

            half SampleDensity(float2 worldXZ, float heightWS)
            {
                float2 scroll = GetFlowScroll();
                float2 uvA = worldXZ * _Tiling + scroll + float2(0.0, heightWS * _VerticalDrift);
                float2 uvB = worldXZ * (_Tiling * _DetailTiling) + scroll;

                // Procedural primary (seamless) + optional texture detail
                half proc = (half)Fbm(uvA * 3.0);
                half texA = SAMPLE_TEXTURE2D(_NoiseTexA, sampler_NoiseTexA, uvA).r;
                half texB = SAMPLE_TEXTURE2D(_NoiseTexB, sampler_NoiseTexB, uvB).r;
                half tex = lerp(texA, texB, _NoiseBlend);
                half blended = lerp(proc, tex, 0.35h);
                return RemapContrast(blended);
            }

            float SoftDepthFactor(float4 screenPos, float3 positionWS)
            {
                float2 screenUV = screenPos.xy / max(screenPos.w, 1e-5);
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneEye = LinearEyeDepth(rawDepth, _ZBufferParams);
                float3 viewPos = TransformWorldToView(positionWS);
                float thisEye = max(-viewPos.z, 1e-3);
                return saturate((sceneEye - thisEye) / max(_SoftDepthFade, 1e-3));
            }

            float CameraDistanceFade(float3 positionWS)
            {
                float dist = distance(positionWS, GetCameraPositionWS());
                float nearF = saturate(dist / max(_NearFade, 1e-3));
                float farF = 1.0 - saturate((dist - _FarFadeStart) / max(_FarFadeEnd - _FarFadeStart, 1e-3));
                return nearF * farF;
            }

            float SheetEdgeFade(float2 uv)
            {
                float2 d = abs(uv - 0.5) * 2.0;
                float m = max(d.x, d.y);
                return 1.0 - smoothstep(1.0 - _EdgeFade, 1.0, m);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(pos.positionCS);
                output.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half densityMask = SampleDensity(input.positionWS.xz, input.positionWS.y);
                half alpha = pow(densityMask, _AlphaPower) * _Density * _HeightFade;

                alpha *= SoftDepthFactor(input.screenPos, input.positionWS);
                alpha *= CameraDistanceFade(input.positionWS);
                alpha *= SheetEdgeFade(input.uv);
                alpha = saturate(alpha);

                half3 col = lerp(_ColorFar.rgb, _ColorNear.rgb, densityMask);
                col = MixFog(col, input.fogFactor);

                return half4(col, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
