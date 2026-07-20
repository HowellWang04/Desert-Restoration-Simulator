# Economy System

## Toggle

- `EconomySystem` / `DesertEconomySystem` → **Enable Economy**
- Or DecisionUI toggle「启用金钱系统」

When **off**: no costs, no income.

## Default balance (can change in Inspector)

| Item | Value |
|------|-------|
| Starting money | 8000 |
| Tree cost | 2 / tree |
| Factory build | 1200 |
| Factory income | **1.5/s** 1st factory, decay + soft cap **6/s** |
| Factory pollution | **0.6 trees / factory / s**, settle every **2.5s** |
| Mining | one button only（不可撤销）; cost 450 → 900 in 3 slow stages |

With factories standing, pollution kills trees faster than one factory can fund planting → must **demolish factories** before finishing clear sky.

## Setup

**Sand → Setup Environment Decision UI** (adds EconomySystem on EnvironmentSystem).  
Prefer delete old `DecisionUI` then re-run for clean money UI.
