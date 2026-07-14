#ifndef SAND_WIND_COMMON_INCLUDED
#define SAND_WIND_COMMON_INCLUDED

// Shared wind globals from WindManager
float4 _SandGlobalWind; // xy = dir, z = speed, w = active

float2 SandWind_GetDir(float2 materialDir, float useLocal)
{
    float2 dir = useLocal > 0.5
        ? materialDir
        : (_SandGlobalWind.w > 0.5 ? _SandGlobalWind.xy : materialDir);
    float lenSq = dot(dir, dir);
    return lenSq > 1e-6 ? dir * rsqrt(lenSq) : float2(1.0, 0.0);
}

float SandWind_GetSpeed(float materialSpeed, float useLocal)
{
    if (useLocal > 0.5)
        return max(materialSpeed, 0.0);
    return _SandGlobalWind.w > 0.5 ? max(_SandGlobalWind.z, 0.0) : max(materialSpeed, 0.0);
}

#endif
