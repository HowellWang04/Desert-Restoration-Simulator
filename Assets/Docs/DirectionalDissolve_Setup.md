# Directional Dissolve

Wind-driven dissolve for sand covering / blowing away objects. Compatible with `WindManager` + Ground Sand globals.

## Quick Preview

**Sand → Setup Directional Dissolve Preview**

Then **Play**. A cube dissolves along the wind with an edge glow.

## Modes

| Mode | Effect |
|------|--------|
| **BlowAway** | Object clips away along wind (sand erodes / carries it off) |
| **Cover** | Object is buried by sand color along wind |

## Key Parameters

| Property | Role |
|----------|------|
| Dissolve Strength | Progress 0→1 |
| Wind Direction | From WindManager (or local) |
| Noise Texture + World Pos | Irregular dissolve edge |
| Edge Color / Width / Intensity | Hot sand rim |
| Dissolve Scale | How strongly world axis maps to dissolve |
| Invert Wind Axis | Flip advance direction |

## Setup on your own mesh

1. Create Material → Shader `Sand/DirectionalDissolve`
2. Assign Base Map + Dissolve Noise (use `SandPreview` noise)
3. Assign to Renderer, add `DirectionalDissolveController`
4. Ensure scene has `WindSystem` (`WindManager`)

## Formula (simplified)

```
field = saturate(dot(worldXZ - origin, windDir) * scale * 0.5 + 0.5 + noise)
BlowAway: clip(field - strength)
Cover:    lerp(base, sand, coverFactor)
edge = 1 - sat(|field - strength| / edgeWidth)
```
