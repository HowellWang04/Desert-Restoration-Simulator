Shader "Sand/DesertSkybox"
{
    Properties
    {
        _DustSkyColor ("Dust Sky", Color) = (0.72, 0.55, 0.32, 1)
        _DustHorizonColor ("Dust Horizon", Color) = (0.85, 0.68, 0.42, 1)
        _ClearSkyColor ("Clear Sky", Color) = (0.28, 0.55, 0.92, 1)
        _ClearHorizonColor ("Clear Horizon", Color) = (0.72, 0.86, 0.98, 1)
        _CloudColor ("Cloud Color", Color) = (1, 1, 1, 1)
        _CloudCoverage ("Cloud Coverage", Range(0, 1)) = 0.45
        _CloudScale ("Cloud Scale", Range(0.2, 8)) = 2.2
        _CloudSpeed ("Cloud Speed", Range(0, 0.2)) = 0.012
        _Clearness ("Clearness 0=dust 1=blue", Range(0, 1)) = 0
        _Exposure ("Exposure", Range(0.2, 2)) = 1.05
        _NoiseTex ("Cloud Noise", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 viewDir : TEXCOORD0;
            };

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _DustSkyColor;
                float4 _DustHorizonColor;
                float4 _ClearSkyColor;
                float4 _ClearHorizonColor;
                float4 _CloudColor;
                float _CloudCoverage;
                float _CloudScale;
                float _CloudSpeed;
                float _Clearness;
                float _Exposure;
                float4 _NoiseTex_ST;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                // Skybox mesh is already in view space style; use clip from object
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.viewDir = v.positionOS.xyz;
                return o;
            }

            float CloudSample(float2 uv, float t)
            {
                float2 u1 = uv * _CloudScale + float2(t * _CloudSpeed, t * _CloudSpeed * 0.35);
                float2 u2 = uv * (_CloudScale * 2.1) + float2(-t * _CloudSpeed * 0.7, t * _CloudSpeed * 0.5);
                float n1 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, u1).r;
                float n2 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, u2).r;
                float n = saturate(n1 * 0.65 + n2 * 0.45);
                float edge = lerp(0.55, 0.28, _CloudCoverage);
                return smoothstep(edge, edge + 0.22, n);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 dir = normalize(i.viewDir);
                float h = saturate(dir.y * 0.5 + 0.5); // -1..1 -> 0..1
                float up = saturate(dir.y);

                float clear = saturate(_Clearness);
                float3 dustCol = lerp(_DustHorizonColor.rgb, _DustSkyColor.rgb, pow(h, 0.85));
                float3 clearCol = lerp(_ClearHorizonColor.rgb, _ClearSkyColor.rgb, pow(saturate(dir.y * 0.85 + 0.15), 0.7));
                // 线性 Clearness 直接驱动黄→蓝，避免额外曲线造成“突然变蓝”
                float3 sky = lerp(dustCol, clearCol, clear);

                float2 cloudUV = dir.xz / max(0.15, abs(dir.y) + 0.35) * 0.5 + 0.5;
                float clouds = CloudSample(cloudUV, _TimeParameters.x) * saturate(up * 1.8);
                // 白云主要由 CloudCoverage 控制，不再被 Clearness 二次掐死
                sky = lerp(sky, _CloudColor.rgb, clouds * saturate(_CloudCoverage));

                sky *= _Exposure;
                return half4(sky, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
