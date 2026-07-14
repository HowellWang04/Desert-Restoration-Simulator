# Environment Decision System

## Setup

1. Ensure scene has `WindSystem`, `AirborneSand`, `HeatSystem`.
2. Menu: **Sand → Setup Environment Decision UI**
3. Play. Use left panel buttons to change Tree / Factory / Mining.

## Formula

```
t = T / Tmax,  f = F / Fmax,  m = M / Mmax
E = clamp(0.55*t - 0.30*f - 0.25*m + 0.50, 0, 1)
```

FX via `DesertFxAPI`:
- Wind strength: `lerp(Bad, Base, E)`
- Wind direction: only if **Enable Auto Wind Direction** is on
- Sand density/speed: better E → sparser/slower
- Heat: `lerp(Bad=0.02, Base, E) * (1 + 0.4 * f)`  (factories add extra heat)
- Tree max = 50000, plant/remove step = 10

## Hook points for real gameplay

In `DesertEnvironmentUI.cs`:
- `OnPlantTreeClicked` — TODO for real plant tree
- `OnBuildFactoryClicked` — TODO for real build factory
- `OnStartMiningClicked` — TODO for real mining

Currently buttons only update decision counts so you can tune FX first.
