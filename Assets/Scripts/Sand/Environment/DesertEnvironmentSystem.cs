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
            treeCount = Mathf.Clamp(value, 0, Mathf.Max(1, config.treeMax));
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
            treeCount = Mathf.Clamp(treeCount, 0, Mathf.Max(1, config.treeMax));
            factoryCount = Mathf.Clamp(factoryCount, 0, Mathf.Max(1, config.factoryMax));
            miningCount = Mathf.Clamp(miningCount, 0, Mathf.Max(1, config.miningMax));
        }

        private void RecalculateEnvironment()
        {
            normalizedTree = config.treeMax > 0 ? Mathf.Clamp01((float)treeCount / config.treeMax) : 0f;
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
                heatDistortionStrength = heat
            };
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
                heatDistortionStrength = Mathf.Lerp(a.heatDistortionStrength, b.heatDistortionStrength, t)
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
