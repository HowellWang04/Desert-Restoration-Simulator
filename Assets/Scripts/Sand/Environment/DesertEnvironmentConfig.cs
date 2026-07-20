using UnityEngine;

namespace SandSystem
{
    /// <summary>
    /// 环境指数与特效映射的可调配置（挂在 DesertEnvironmentSystem 上）。
    /// </summary>
    [System.Serializable]
    public class DesertEnvironmentConfig
    {
        [Header("Decision Caps")]
        [Tooltip("树木数量上限。0 = 不限制")]
        public int treeMax = 0;
        public int factoryMax = 20;
        public int miningMax = 100;

        [Header("Environment Weights (E)")]
        [Range(0f, 1f)] public float weightTree = 0.55f;
        [Range(0f, 1f)] public float weightFactory = 0.30f;
        [Range(0f, 1f)] public float weightMining = 0.25f;
        [Range(0f, 1f)] public float environmentBaseline = 0.50f;

        [Header("Wind")]
        public float baseWindStrength = 1.0f;
        public float badWindStrength = 2.2f;
        public Vector2 baseWindDirection = new Vector2(1f, 0.25f);
        public Vector2 badWindDirection = new Vector2(1f, -0.15f);
        [Tooltip("开启后，环境越差风向越偏向 Bad Wind Direction")]
        public bool enableAutoWindDirection = true;

        [Header("Sand Global")]
        public float baseSandGlobalDensity = 0.5f;
        public float badSandGlobalDensity = 1.4f;

        [Header("Sand Layer Density (Near / Mid / Far)")]
        public float baseNearDensity = 0.35f;
        public float baseMidDensity = 0.25f;
        public float baseFarDensity = 0.15f;
        public float badNearDensity = 0.8f;
        public float badMidDensity = 0.6f;
        public float badFarDensity = 0.4f;

        [Header("Sand Layer Speed (Near / Mid / Far)")]
        public float baseNearSpeed = 1.2f;
        public float baseMidSpeed = 0.9f;
        public float baseFarSpeed = 0.6f;
        public float badNearSpeed = 1.8f;
        public float badMidSpeed = 1.35f;
        public float badFarSpeed = 0.95f;
        [Tooltip("层速度是否再乘以 (当前风速 / 基准风速)")]
        public bool scaleLayerSpeedByWind = true;

        [Header("Heat")]
        public float baseHeatStrength = 0.008f;
        [Tooltip("最糟环境热浪强度上限（建议从 0.02 起）")]
        public float badHeatStrength = 0.02f;
        [Tooltip("工厂额外热浪：Heat *= (1 + factoryHeatBonus * f)")]
        public float factoryHeatBonus = 0.4f;

        [Header("Clear Sky (蓝天白云)")]
        [Tooltip("天空盒第一段终点绿植数：0→此处 Clearness/Cloud 线性到 Start 值")]
        public int clearSkySkyStartTrees = 10000;
        [Tooltip("完全蓝天白云的绿植数")]
        public int clearSkyTreeThreshold = 15000;
        [Tooltip("Start 时天空 Clearness")]
        [Range(0f, 1f)] public float clearSkyStartClearness = 0.3f;
        [Tooltip("End 时天空 Clearness")]
        [Range(0f, 1f)] public float clearSkyEndClearness = 1f;
        [Tooltip("Start 时 Cloud Coverage")]
        [Range(0f, 1f)] public float clearSkyStartCloudCoverage = 0.35f;
        [Tooltip("End 时 Cloud Coverage")]
        [Range(0f, 1f)] public float clearSkyEndCloudCoverage = 0.65f;
        [Tooltip("开始明显减弱热浪的绿植数")]
        public int clearSkyHeatStartTrees = 13500;
        [Tooltip("开始消减空中沙尘的绿植数")]
        public int clearSkySandStartTrees = 12800;
        [Tooltip("晴空时热浪强度（接近无）")]
        public float clearSkyHeatStrength = 0.0015f;

        [Header("Transition")]
        [Tooltip("特效数值平滑过渡秒数，0 = 立即")]
        public float blendSeconds = 1.8f;
    }
}
