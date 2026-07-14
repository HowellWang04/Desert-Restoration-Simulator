#ifndef GROUND_SAND_COMMON_INCLUDED
#define GROUND_SAND_COMMON_INCLUDED

// ---------------------------------------------------------------------------
// Ground Sand — 共享逻辑
// 流动：GPU 连续时间 _TimeParameters.x * 风向 * 风速（不依赖每帧 CPU 累加）
// ---------------------------------------------------------------------------

float4 _SandFlowUV;      // legacy / unused for scroll
float4 _SandGlobalWind;  // xy dir, z speed, w active

float2 GroundSand_GetWindDir()
{
    float2 dir = _UseLocalWind > 0.5
        ? _WindDirection.xy
        : (_SandGlobalWind.w > 0.5 ? _SandGlobalWind.xy : _WindDirection.xy);
    float lenSq = dot(dir, dir);
    return lenSq > 1e-6 ? dir * rsqrt(lenSq) : float2(1.0, 0.0);
}

float GroundSand_GetWindSpeed()
{
    if (_UseLocalWind > 0.5)
        return max(_WindSpeed, 0.0);
    return _SandGlobalWind.w > 0.5 ? max(_SandGlobalWind.z, 0.0) : max(_WindSpeed, 0.0);
}

// 单向连续滚动：时间单调递增，帧卡顿时 UV 仍落到正确位置（不会“停一下”）
float2 GroundSand_FlowScroll(float layerSpeed)
{
    float2 dir = GroundSand_GetWindDir();
    float spd = GroundSand_GetWindSpeed();
    return dir * (_TimeParameters.x * spd * _SpeedScale * layerSpeed);
}

half GroundSand_RemapContrast(half n, half contrast)
{
    return saturate((n - 0.5h) * contrast + 0.5h);
}

half GroundSand_SampleNoiseMask(float2 uvA, float2 uvB)
{
    half noiseA = SAMPLE_TEXTURE2D(_NoiseTexA, sampler_NoiseTexA, uvA).r;
    half noiseB = SAMPLE_TEXTURE2D(_NoiseTexB, sampler_NoiseTexB, uvB).r;
    half blended = lerp(noiseA, noiseB, _NoiseBlend);
    half detail = saturate(noiseA * noiseB * 2.0h);
    return GroundSand_RemapContrast(lerp(blended, detail, 0.35h), _Contrast);
}

half3 GroundSand_GradientColor(half sandMask)
{
    half3 colorLow  = lerp(_ColorDeep.rgb,  _ColorMid.rgb,    saturate(sandMask * 2.0h));
    half3 colorHigh = lerp(_ColorMid.rgb,   _ColorBright.rgb, saturate(sandMask * 2.0h - 1.0h));
    return lerp(colorLow, colorHigh, smoothstep(0.45h, 0.55h, sandMask));
}

half4 GroundSand_EvalLayer(
    float2 baseUV,
    float tiling,
    float speed,
    half layerAlpha,
    half3 layerTint)
{
    float2 scroll = GroundSand_FlowScroll(speed);
    float2 uvA = baseUV * tiling + scroll;
    float2 uvB = baseUV * (tiling * 1.73) + scroll;

    half mask = GroundSand_SampleNoiseMask(uvA, uvB);
    half3 rgb = GroundSand_GradientColor(mask) * layerTint;
    half a = saturate(pow(saturate(mask), _AlphaPower)) * layerAlpha;
    return half4(rgb, a);
}

half4 GroundSand_ComposeParallaxSand(float2 baseUV, float3 viewDirWS)
{
    half4 farL = GroundSand_EvalLayer(
        baseUV, _FarTiling, _FarSpeed, _FarAlpha, _FarTint.rgb);

    half4 midL = GroundSand_EvalLayer(
        baseUV, _MidTiling, _MidSpeed, _MidAlpha, _MidTint.rgb);

    half4 nearL = GroundSand_EvalLayer(
        baseUV, _NearTiling, _NearSpeed, _NearAlpha, _NearTint.rgb);

    half3 color = lerp(_ColorDeep.rgb, farL.rgb, saturate(farL.a + 0.35h));
    color = lerp(color, midL.rgb, midL.a);
    color = lerp(color, nearL.rgb, nearL.a);

    if (_ParallaxAmplitude > 1e-5)
    {
        float3 v = viewDirWS;
        float shade = saturate(0.85 + 0.15 * saturate(v.y) * _ParallaxAmplitude * 10.0);
        color *= shade;
    }

    half alpha = saturate(max(farL.a, max(midL.a, nearL.a)) * _Alpha);
    alpha = saturate((alpha - _AlphaCutoff) / max(1e-3h, 1.0h - _AlphaCutoff));
    return half4(color, alpha);
}

#endif
