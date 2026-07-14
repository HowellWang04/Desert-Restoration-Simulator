# World Placement (Tree / Factory / Mining)

## Setup

1. Prefabs under:
   - `Assets/Resources/_Prefab/tree`
   - `Assets/Resources/_Prefab/factory`
2. Menu: **Sand → Setup World Placement** (or **Setup Environment Decision UI**)

## Behavior

### Tree
- Each plant click: spawn **10** trees (UI `treeStep`)
- Random free grid cell, random prefab
- Scale grows from 0 → random **0.2~1**
- Remove: delete last N trees

### Factory
- Clustered around first factory / existing plants
- Never overlap other factories
- Prefer not to cover trees; if no space, cover trees → delete them → UI tree count syncs down

### Mining
- Random circle in play area, delete trees inside
- Mining counter +1; tree UI syncs to alive trees
