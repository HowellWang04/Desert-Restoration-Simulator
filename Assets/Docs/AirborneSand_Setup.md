# Airborne Sand（空中扬沙层）

高性能空中沙幕：多层水平透明 Quad + 深度软边，跟随 `WindManager` 全局流向。

## 一键预览

**Sand → Setup Airborne Sand Preview**

会生成：

- `AirborneSand`（3 层：近 / 中 / 远高度）
- `M_AirborneSand.mat`（`Sand/AirborneSand`）
- 自动确保 `WindSystem` 存在

然后 **Play**。

## 原理

1. **三层水平沙幕**：不同高度、Tiling、Speed、Density  
2. **流向**：复用地表同一套 `_SandFlowUV`（只沿风向前进）  
3. **软粒子深度**：对比场景 Depth Texture，贴地/贴物体处淡出  
4. **距离 / 边缘淡出**：避免硬边和贴镜头  

## 可调参数

| 参数 | 作用 |
|------|------|
| Density | 整体浓度 |
| Soft Depth Fade | 与地形/物体相交软边宽度 |
| Layer Speed | 该层相对流速 |
| Tiling | 噪声密度 |
| Height | 层高度 |
| Color Near / Far | 沙色 |

## 注意

- URP Asset 需 **Depth Texture = On**（本项目 `PC_RPAsset` 已开）  
- 需要场景里有 `WindManager`，否则沙幕几乎不流动  
- 透明队列，大量重叠时注意 Overdraw；预览默认 3 层足够  
