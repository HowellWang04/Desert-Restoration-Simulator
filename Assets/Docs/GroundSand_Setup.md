# Ground Sand（Terrain + Parallax）使用说明

## 一键预览

菜单：**Sand → Setup Ground Sand Terrain Preview**

会生成：

- `SandTerrain`（可雕刻高度）
- `M_GroundSandTerrain` 材质（`Sand/GroundSandTerrain`）
- 噪声贴图、`WindSystem`、`GroundSandController`

然后 **Play**，并用 Terrain 的 Raise/Lower 改高度。

---

## Shader 选择

| Shader | 用途 |
|--------|------|
| `Sand/GroundSandTerrain` | **Terrain 专用（推荐）**，Opaque + Draw Instanced |
| `Sand/GroundSand` | Plane/Mesh 透明版（旧预览仍可用） |

---

## Parallax 层参数（近 / 中 / 远）

每层可调：**Tiling / Speed / Parallax Depth / Alpha / Tint**

| 层 | 建议 | 观感 |
|----|------|------|
| Far | 慢、低视差、低 Alpha | 远处底沙 |
| Mid | 中速、中视差 | 主体流动 |
| Near | 快、高视差、高 Alpha | 近处风沙 |

全局：`Parallax Amplitude`、`Parallax Height Scale`、`World UV Scale`、`Wind Speed`

---

## 手动挂到已有 Terrain

1. 创建材质，Shader 选 `Sand/GroundSandTerrain`
2. 赋两张 Noise（sRGB 关，Repeat）
3. Terrain → Material → 拖入该材质
4. 勾选 **Draw Instanced**
5. 挂 `GroundSandController` + 场景放 `WindManager`

---

## 性能说明

- 单 Pass、无 Tessellation / Geometry Shader
- 每层约 4 次噪声采样（含一次视差高度校正），共约 12 次
- 适合 URP Forward；大地形请保持 Draw Instanced
