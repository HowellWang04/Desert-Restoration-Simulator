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
        public int treeMax = 50000;
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

        [Header("Transition")]
        [Tooltip("特效数值平滑过渡秒数，0 = 立即")]
        public float blendSeconds = 1.25f;
    }
}
