# Clear Sky & 9 Terrains

## Terrains (9)

Menu: **Sand → Expand Terrains to 3x3 (9 tiles)**  
(or **Setup Ground Sand Terrain Preview**)

- 3×3 tiles, each 60×60 m → about **180×180 m**
- Named `SandTerrain_0_0` … `SandTerrain_2_2`
- Center keeps `TD_SandPreview` if it already existed
- Heights use **world-space** noise + edge stitch so neighbors match

If seams appear: **Sand → Restitch Terrain Heights (seamless)**

## Skybox

Menu: **Sand → Setup Desert Skybox**

- Material: `Assets/SandPreview/M_DesertSkybox.mat`
- Scene object: `SkySystem` (`SkyboxController`)

## Clear sky trigger

On `EnvironmentSystem` → config:

| Field | Default | Meaning |
|-------|---------|---------|
| Clear Sky Tree Threshold | **4000** | Trees needed for full blue sky |
| Clear Sky Blend Start Ratio | 0.7 | Starts fading from 2800 trees |
| Clear Sky Heat Strength | 0.002 | Heat when clear |

When blend → 1:
1. Airborne sand density → **0**
2. Heat → clear-sky low value
3. Skybox Clearness → **1** (blue + white clouds)

UI shows `· 晴空` / `· 晴空过渡 xx%` next to E.
