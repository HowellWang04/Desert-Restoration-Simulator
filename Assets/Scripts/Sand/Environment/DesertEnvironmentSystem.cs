using UnityEngine;

namespace SandSystem
{
    /// <summary>
    /// 决策变量 → 环境指数 E → 风/沙/热浪特效。
    /// 场景中建议命名为 EnvironmentSystem。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sand/Desert Environment System")]
    public class DesertEnvironmentSystem : MonoBehaviour
    {
        public static DesertEnvironmentSystem Instance { get; private set; }

        [Header("Config")]
        [SerializeField] private DesertEnvironmentConfig config = new DesertEnvironmentConfig();

        [Header("Decision State")]
        [SerializeField] private int treeCount;
        [SerializeField] private int factoryCount;
        [SerializeField] private int miningCount;

        [Header("Runtime (Read Only)")]
        [SerializeField] private float environmentIndex;
        [SerializeField] private float normalizedTree;
        [SerializeField] private float normalizedFactory;
        [SerializeField] private float normalizedMining;

        private DesertFxAPI.FxSnapshot _from;
        private DesertFxAPI.FxSnapshot _to;
        private float _blendT = 1f;
        private bool _blending;

        public DesertEnvironmentConfig Config => config;

        public int TreeCount => treeCount;
        public int FactoryCount => factoryCount;
        public int MiningCount => miningCount;
        public float EnvironmentIndex => environmentIndex;
        public float NormalizedTree => normalizedTree;
        public float NormalizedFactory => normalizedFactory;
        public float NormalizedMining => normalizedMining;

        public bool EnableAutoWindDirection
        {
            get => config.enableAutoWindDirection;
            set
            {
                if (config.enableAutoWindDirection == value) return;
                config.enableAutoWindDirection = value;
                RefreshFx(immediate: false);
            }
        }

        public event System.Action OnEnvironmentChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[DesertEnvironmentSystem] Duplicate instance, destroying this.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            ClampCounts();
            RecalculateEnvironment();
            CaptureCurrentAsBlendStart();
            _to = BuildTargetSnapshot();
            ApplySnapshotImmediate(_to);
            _blendT = 1f;
            _blending = false;
            OnEnvironmentChanged?.Invoke();
        }

        private void Update()
        {
            if (!_blending || config.blendSeconds <= 0.0001f) return;

            _blendT += Time.deltaTime / config.blendSeconds;
            if (_blendT >= 1f)
            {
                _blendT = 1f;
                _blending = false;
                ApplySnapshotImmediate(_to);
                return;
            }

            ApplySnapshotImmediate(LerpSnapshot(_from, _to, Smooth01(_blendT)));
        }

        // -------- Decision setters (UI / gameplay) --------

        public void SetTreeCount(int value)
        {
            if (config.treeMax > 0)
                treeCount = Mathf.Clamp(value, 0, config.treeMax);
            else
                treeCount = Mathf.Max(0, value);
            RefreshFx(immediate: false);
        }

        public void SetFactoryCount(int value)
        {
            factoryCount = Mathf.Clamp(value, 0, Mathf.Max(1, config.factoryMax));
            RefreshFx(immediate: false);
        }

        public void SetMiningCount(int value)
        {
            miningCount = Mathf.Clamp(value, 0, Mathf.Max(1, config.miningMax));
            RefreshFx(immediate: false);
        }

        public void AddTrees(int delta) => SetTreeCount(treeCount + delta);
        public void AddFactories(int delta) => SetFactoryCount(factoryCount + delta);
        public void AddMining(int delta) => SetMiningCount(miningCount + delta);

        /// <summary>晴空所需绿植数量（UI / Inspector 可改）。</summary>
        public int ClearSkyTreeThreshold
        {
            get => Mathf.Max(1, config.clearSkyTreeThreshold);
            set
            {
                int v = Mathf.Max(1, value);
                if (config.clearSkyTreeThreshold == v) return;
                config.clearSkyTreeThreshold = v;
                RefreshFx(immediate: false);
            }
        }

        /// <summary>立刻重算 E 并推到特效（可跳过过渡）。</summary>
        public void RefreshFx(bool immediate = false)
        {
            ClampCounts();
            RecalculateEnvironment();

            if (immediate || config.blendSeconds <= 0.0001f)
            {
                _to = BuildTargetSnapshot();
                ApplySnapshotImmediate(_to);
                _blendT = 1f;
                _blending = false;
            }
            else
            {
                CaptureCurrentAsBlendStart();
                _to = BuildTargetSnapshot();
                _blendT = 0f;
                _blending = true;
            }

            OnEnvironmentChanged?.Invoke();
        }

        // -------- Core formula --------

        private void ClampCounts()
        {
            if (config.treeMax > 0)
                treeCount = Mathf.Clamp(treeCount, 0, config.treeMax);
            else
                treeCount = Mathf.Max(0, treeCount);

            factoryCount = Mathf.Clamp(factoryCount, 0, Mathf.Max(1, config.factoryMax));
            miningCount = Mathf.Clamp(miningCount, 0, Mathf.Max(1, config.miningMax));
        }

        private void RecalculateEnvironment()
        {
            // 树木无上限时，用晴空阈值作为环境指数归一化参考
            int treeRef = config.treeMax > 0
                ? config.treeMax
                : Mathf.Max(1, config.clearSkyTreeThreshold);
            normalizedTree = Mathf.Clamp01((float)treeCount / treeRef);
            normalizedFactory = config.factoryMax > 0 ? Mathf.Clamp01((float)factoryCount / config.factoryMax) : 0f;
            normalizedMining = config.miningMax > 0 ? Mathf.Clamp01((float)miningCount / config.miningMax) : 0f;

            // E = clamp(wT*t - wF*f - wM*m + baseline, 0, 1)
            environmentIndex = Mathf.Clamp01(
                config.weightTree * normalizedTree
                - config.weightFactory * normalizedFactory
                - config.weightMining * normalizedMining
                + config.environmentBaseline);
        }

        public DesertFxAPI.FxSnapshot BuildTargetSnapshot()
        {
            float e = environmentIndex;
            float f = normalizedFactory;
            var c = config;

            float windStrength = Mathf.Lerp(c.badWindStrength, c.baseWindStrength, e);
            Vector2 windDir = c.baseWindDirection;
            if (c.enableAutoWindDirection)
            {
                windDir = Vector2.Lerp(c.badWindDirection, c.baseWindDirection, e);
            }
            else
            {
                // 关闭自动风向时保持当前风向，避免每次刷新都把风向改回去
                windDir = DesertFxAPI.GetWindDirection();
                if (windDir.sqrMagnitude < 0.0001f)
                    windDir = c.baseWindDirection;
            }

            float sandGlobal = Mathf.Lerp(c.badSandGlobalDensity, c.baseSandGlobalDensity, e);
            float nearD = Mathf.Lerp(c.badNearDensity, c.baseNearDensity, e);
            float midD = Mathf.Lerp(c.badMidDensity, c.baseMidDensity, e);
            float farD = Mathf.Lerp(c.badFarDensity, c.baseFarDensity, e);

            float nearS = Mathf.Lerp(c.badNearSpeed, c.baseNearSpeed, e);
            float midS = Mathf.Lerp(c.badMidSpeed, c.baseMidSpeed, e);
            float farS = Mathf.Lerp(c.badFarSpeed, c.baseFarSpeed, e);

            if (c.scaleLayerSpeedByWind && c.baseWindStrength > 0.0001f)
            {
                float windRatio = windStrength / c.baseWindStrength;
                nearS *= windRatio;
                midS *= windRatio;
                farS *= windRatio;
            }

            // 热浪：环境越差越强，工厂再额外加重
            float heat = Mathf.Lerp(c.badHeatStrength, c.baseHeatStrength, e);
            heat *= 1f + c.factoryHeatBonus * f;

            // 晴空：天空严格线性；热浪/沙尘仍分曲线
            float heatBlend = EvaluateLinear01(treeCount, c.clearSkyHeatStartTrees, c.clearSkyTreeThreshold);
            float sandBlend = EvaluateLinear01(treeCount, c.clearSkySandStartTrees, c.clearSkyTreeThreshold);

            if (sandBlend > 0.0001f)
            {
                sandGlobal = Mathf.Lerp(sandGlobal, 0f, sandBlend);
                nearD = Mathf.Lerp(nearD, 0f, sandBlend);
                midD = Mathf.Lerp(midD, 0f, sandBlend);
                farD = Mathf.Lerp(farD, 0f, sandBlend);
            }

            if (heatBlend > 0.0001f)
                heat = Mathf.Lerp(heat, c.clearSkyHeatStrength, heatBlend);

            EvaluateSkyboxParams(treeCount, c, out float skyClearness, out float skyCloudCoverage);

            return new DesertFxAPI.FxSnapshot
            {
                windDirection = windDir,
                windStrength = windStrength,
                sandGlobalDensity = sandGlobal,
                sandNearDensity = nearD,
                sandMidDensity = midD,
                sandFarDensity = farD,
                sandNearSpeed = nearS,
                sandMidSpeed = midS,
                sandFarSpeed = farS,
                heatDistortionStrength = heat,
                skyClearness = skyClearness,
                skyCloudCoverage = skyCloudCoverage
            };
        }

        /// <summary>
        /// 天空盒两段线性：
        /// 0 → startTrees：Clearness/Cloud 0 → Start 值；
        /// startTrees → endTrees：Start 值 → End 值。
        /// </summary>
        public static void EvaluateSkyboxParams(int trees, DesertEnvironmentConfig c, out float clearness, out float cloudCoverage)
        {
            int start = Mathf.Max(1, c.clearSkySkyStartTrees);
            int end = Mathf.Max(start + 1, c.clearSkyTreeThreshold);
            trees = Mathf.Max(0, trees);

            float startClear = Mathf.Clamp01(c.clearSkyStartClearness);
            float endClear = Mathf.Clamp01(c.clearSkyEndClearness);
            float startCloud = Mathf.Clamp01(c.clearSkyStartCloudCoverage);
            float endCloud = Mathf.Clamp01(c.clearSkyEndCloudCoverage);

            if (trees <= 0)
            {
                clearness = 0f;
                cloudCoverage = 0f;
                return;
            }

            if (trees < start)
            {
                // 0 → 10000：0 → 0.3 / 0 → 0.35
                float t0 = trees / (float)start;
                clearness = Mathf.Lerp(0f, startClear, t0);
                cloudCoverage = Mathf.Lerp(0f, startCloud, t0);
                return;
            }

            // 10000 → 15000：0.3 → 1 / 0.35 → 1
            float t1 = EvaluateLinear01(trees, start, end);
            clearness = Mathf.Lerp(startClear, endClear, t1);
            cloudCoverage = Mathf.Lerp(startCloud, endCloud, t1);
        }

        /// <summary>天空过渡总进度 0~1（整段 0→15000，用于 UI）。</summary>
        public float EvaluateClearSkyBlend()
        {
            int end = Mathf.Max(1, config.clearSkyTreeThreshold);
            return Mathf.Clamp01(treeCount / (float)end);
        }

        public bool IsClearSkyActive =>
            treeCount >= Mathf.Max(1, config.clearSkyTreeThreshold);

        /// <summary>纯线性 0~1，不做 smoothstep（避免中段陡跳）。</summary>
        private static float EvaluateLinear01(int trees, int start, int end)
        {
            if (end <= start) end = start + 1;
            if (trees <= start) return 0f;
            if (trees >= end) return 1f;
            return (trees - start) / (float)(end - start);
        }

        private void CaptureCurrentAsBlendStart()
        {
            _from = DesertFxAPI.CaptureSnapshot();
        }

        private static void ApplySnapshotImmediate(DesertFxAPI.FxSnapshot s)
        {
            DesertFxAPI.ApplySnapshot(s);
        }

        private static DesertFxAPI.FxSnapshot LerpSnapshot(DesertFxAPI.FxSnapshot a, DesertFxAPI.FxSnapshot b, float t)
        {
            return new DesertFxAPI.FxSnapshot
            {
                windDirection = Vector2.Lerp(a.windDirection, b.windDirection, t),
                windStrength = Mathf.Lerp(a.windStrength, b.windStrength, t),
                sandGlobalDensity = Mathf.Lerp(a.sandGlobalDensity, b.sandGlobalDensity, t),
                sandNearDensity = Mathf.Lerp(a.sandNearDensity, b.sandNearDensity, t),
                sandMidDensity = Mathf.Lerp(a.sandMidDensity, b.sandMidDensity, t),
                sandFarDensity = Mathf.Lerp(a.sandFarDensity, b.sandFarDensity, t),
                sandNearSpeed = Mathf.Lerp(a.sandNearSpeed, b.sandNearSpeed, t),
                sandMidSpeed = Mathf.Lerp(a.sandMidSpeed, b.sandMidSpeed, t),
                sandFarSpeed = Mathf.Lerp(a.sandFarSpeed, b.sandFarSpeed, t),
                heatDistortionStrength = Mathf.Lerp(a.heatDistortionStrength, b.heatDistortionStrength, t),
                skyClearness = Mathf.Lerp(a.skyClearness, b.skyClearness, t),
                skyCloudCoverage = Mathf.Lerp(a.skyCloudCoverage, b.skyCloudCoverage, t)
            };
        }

        private static float Smooth01(float t)
        {
            // smoothstep
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying) return;
            ClampCounts();
            RecalculateEnvironment();
        }
#endif
    }
}
