Shader "Hidden/Sand/HeatDistortion"
{
    Properties
    {
        [Header(Distortion)]
        _DistortionStrength ("Distortion Strength", Range(0, 0.1)) = 0.012
        _NoiseScale ("Noise Scale", Float) = 8.0
        _ScrollSpeed ("Scroll Speed", Float) = 0.15
        _WindDirection ("Wind Direction (XY)", Vector) = (1, 0.2, 0, 0)
        _VerticalBias ("Vertical Bias", Range(0, 1)) = 0.65
        _NoiseTex ("Noise Texture", 2D) = "gray" {}

        [Header(Mask)]
        _HeatHeightStart ("Heat Height Start (WS Y)", Float) = 0.5
        _HeatHeightEnd ("Heat Height End (WS Y)", Float) = 12
        _HeatMaskPower ("Height Mask Power", Range(0.2, 4)) = 1.2
        [Toggle] _UseDepthMask ("Use Depth Mask", Float) = 1
        _DepthMaskStart ("Depth Mask Start", Float) = 5
        _DepthMaskEnd ("Depth Mask End", Float) = 80

        [Header(Source)]
        [KeywordEnum(BlitColor, OpaqueTexture)] _SourceMode ("Source Mode", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        Pass
        {
            Name "HeatDistortion"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local _SOURCEMODE_BLITCOLOR _SOURCEMODE_OPAQUETEXTURE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "SandWindCommon.hlsl"

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float _DistortionStrength;
                float _NoiseScale;
                float _ScrollSpeed;
                float4 _WindDirection;
                float _VerticalBias;
                float4 _NoiseTex_ST;
                float _HeatHeightStart;
                float _HeatHeightEnd;
                float _HeatMaskPower;
                float _UseDepthMask;
                float _DepthMaskStart;
                float _DepthMaskEnd;
            CBUFFER_END

            #if defined(_SOURCEMODE_OPAQUETEXTURE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #endif

            float2 GetWindDir2()
            {
                float2 dir = _SandGlobalWind.w > 0.5 ? _SandGlobalWind.xy : _WindDirection.xy;
                float lenSq = dot(dir, dir);
                return lenSq > 1e-6 ? dir * rsqrt(lenSq) : float2(1.0, 0.0);
            }

            float GetWindSpeed()
            {
                return _SandGlobalWind.w > 0.5 ? max(_SandGlobalWind.z, 0.0) : 1.0;
            }

            float2 SampleDistortOffset(float2 uv)
            {
                float2 windDir = GetWindDir2();
                float speed = GetWindSpeed();
                float2 scroll = windDir * (_TimeParameters.x * _ScrollSpeed * speed);

                // Screen UV as noise domain; bias more vertical shimmer for heat haze
                float2 noiseUV = uv * _NoiseScale + scroll;
                float n0 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV).r;
                float n1 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV * 1.73 + float2(17.1, 9.3)).r;
                float2 offset = float2(n0, n1) * 2.0 - 1.0;
                offset.y = lerp(offset.y, abs(offset.y), _VerticalBias);
                return offset * _DistortionStrength;
            }

            // Reconstruct approximate world Y from depth for height mask (optional)
            float ReconstructWorldY(float2 uv, float rawDepth)
            {
                #if UNITY_REVERSED_Z
                if (rawDepth < 1e-5)
                    return _HeatHeightEnd;
                #else
                if (rawDepth > 0.99999)
                    return _HeatHeightEnd;
                #endif

                float3 positionWS = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                return positionWS.y;
            }

            float ComputeHeatMask(float2 uv)
            {
                float mask = 1.0;

                if (_UseDepthMask > 0.5)
                {
                    float rawDepth = SampleSceneDepth(uv);
                    float eyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                    float depthMask = saturate((eyeDepth - _DepthMaskStart) / max(_DepthMaskEnd - _DepthMaskStart, 1e-3));
                    mask *= depthMask;

                    float worldY = ReconstructWorldY(uv, rawDepth);
                    float heightMask = saturate((worldY - _HeatHeightStart) / max(_HeatHeightEnd - _HeatHeightStart, 1e-3));
                    mask *= pow(heightMask, _HeatMaskPower);
                }

                return mask;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float heatMask = ComputeHeatMask(uv);
                float2 offset = SampleDistortOffset(uv) * heatMask;
                float2 distortedUV = saturate(uv + offset);

                #if defined(_SOURCEMODE_OPAQUETEXTURE)
                    half3 col = SampleSceneColor(distortedUV);
                #else
                    // Blit path: color buffer copy provided by Renderer Feature as _BlitTexture
                    half3 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, distortedUV).rgb;
                #endif

                return half4(col, 1);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
