using UnityEngine;
using UnityEngine.UI;

namespace SandSystem
{
    /// <summary>
    /// 三项决策 UI：种树 / 建厂 / 开采。点击后调用真实放置系统。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sand/Desert Environment UI")]
    public class DesertEnvironmentUI : MonoBehaviour
    {
        [Header("System")]
        [SerializeField] private DesertEnvironmentSystem environment;
        [SerializeField] private TreePlacementSystem treePlacement;
        [SerializeField] private FactoryPlacementSystem factoryPlacement;
        [SerializeField] private MiningSystem miningSystem;

        [Header("Labels")]
        [SerializeField] private Text treeLabel;
        [SerializeField] private Text factoryLabel;
        [SerializeField] private Text miningLabel;
        [SerializeField] private Text environmentLabel;
        [SerializeField] private Text windDirHintLabel;

        [Header("Buttons — Tree")]
        [SerializeField] private Button plantTreeButton;
        [SerializeField] private Button removeTreeButton;

        [Header("Buttons — Factory")]
        [SerializeField] private Button buildFactoryButton;
        [SerializeField] private Button removeFactoryButton;

        [Header("Buttons — Mining")]
        [SerializeField] private Button startMiningButton;
        [SerializeField] private Button reduceMiningButton;

        [Header("Options")]
        [SerializeField] private Toggle autoWindDirectionToggle;

        [Header("Step")]
        [SerializeField] private int treeStep = 10;
        [SerializeField] private int factoryStep = 1;
        [SerializeField] private int miningStep = 1;

        private void Awake()
        {
            ResolveRefs();
            WireButtons();
        }

        private void OnEnable()
        {
            if (environment != null)
                environment.OnEnvironmentChanged += RefreshLabels;
            RefreshLabels();
            SyncToggleFromSystem();
        }

        private void OnDisable()
        {
            if (environment != null)
                environment.OnEnvironmentChanged -= RefreshLabels;
        }

        private void ResolveRefs()
        {
            if (environment == null)
                environment = DesertEnvironmentSystem.Instance != null
                    ? DesertEnvironmentSystem.Instance
                    : FindFirstObjectByType<DesertEnvironmentSystem>();

            if (treePlacement == null)
                treePlacement = TreePlacementSystem.Instance != null
                    ? TreePlacementSystem.Instance
                    : FindFirstObjectByType<TreePlacementSystem>();

            if (factoryPlacement == null)
                factoryPlacement = FactoryPlacementSystem.Instance != null
                    ? FactoryPlacementSystem.Instance
                    : FindFirstObjectByType<FactoryPlacementSystem>();

            if (miningSystem == null)
                miningSystem = MiningSystem.Instance != null
                    ? MiningSystem.Instance
                    : FindFirstObjectByType<MiningSystem>();
        }

        private void WireButtons()
        {
            if (plantTreeButton != null)
            {
                plantTreeButton.onClick.RemoveAllListeners();
                plantTreeButton.onClick.AddListener(OnPlantTreeClicked);
            }

            if (removeTreeButton != null)
            {
                removeTreeButton.onClick.RemoveAllListeners();
                removeTreeButton.onClick.AddListener(OnRemoveTreeClicked);
            }

            if (buildFactoryButton != null)
            {
                buildFactoryButton.onClick.RemoveAllListeners();
                buildFactoryButton.onClick.AddListener(OnBuildFactoryClicked);
            }

            if (removeFactoryButton != null)
            {
                removeFactoryButton.onClick.RemoveAllListeners();
                removeFactoryButton.onClick.AddListener(OnRemoveFactoryClicked);
            }

            if (startMiningButton != null)
            {
                startMiningButton.onClick.RemoveAllListeners();
                startMiningButton.onClick.AddListener(OnStartMiningClicked);
            }

            if (reduceMiningButton != null)
            {
                reduceMiningButton.onClick.RemoveAllListeners();
                reduceMiningButton.onClick.AddListener(OnReduceMiningClicked);
            }

            if (autoWindDirectionToggle != null)
            {
                autoWindDirectionToggle.onValueChanged.RemoveAllListeners();
                autoWindDirectionToggle.onValueChanged.AddListener(OnAutoWindDirectionChanged);
            }
        }

        private void SyncToggleFromSystem()
        {
            if (environment == null || autoWindDirectionToggle == null) return;
            autoWindDirectionToggle.SetIsOnWithoutNotify(environment.EnableAutoWindDirection);
        }

        // =====================================================================
        // 决策按钮 → 真实放置
        // =====================================================================

        /// <summary>UI：种树 (+) — 每次长出 treeStep 棵（默认 10）</summary>
        public void OnPlantTreeClicked()
        {
            if (!EnsureSystem()) return;
            ResolveRefs();

            if (treePlacement == null)
            {
                Debug.LogWarning("[DesertEnvironmentUI] TreePlacementSystem missing. Run Sand / Setup World Placement.");
                return;
            }

            // 真实种树：随机位置 / 随机 prefab / 生长缩放
            int room = Mathf.Max(0, environment.Config.treeMax - environment.TreeCount);
            int want = Mathf.Min(treeStep, room);
            if (want <= 0)
            {
                Debug.Log("[DesertEnvironmentUI] Tree count already at max.");
                return;
            }

            int planted = treePlacement.PlantTrees(want);
            SyncTreeCountFromWorld();
            if (planted < want)
                Debug.Log($"[DesertEnvironmentUI] Planted {planted}/{want} (no free cells or prefab missing).");
            RefreshLabels();
        }

        /// <summary>UI：减少树木 (-) — 直接删除最近的树</summary>
        public void OnRemoveTreeClicked()
        {
            if (!EnsureSystem()) return;
            ResolveRefs();

            if (treePlacement == null)
            {
                Debug.LogWarning("[DesertEnvironmentUI] TreePlacementSystem missing.");
                return;
            }

            treePlacement.RemoveTrees(treeStep);
            SyncTreeCountFromWorld();
            RefreshLabels();
        }

        /// <summary>UI：新建厂房 (+)</summary>
        public void OnBuildFactoryClicked()
        {
            if (!EnsureSystem()) return;
            ResolveRefs();

            if (factoryPlacement == null)
            {
                Debug.LogWarning("[DesertEnvironmentUI] FactoryPlacementSystem missing. Run Sand / Setup World Placement.");
                return;
            }

            int room = Mathf.Max(0, environment.Config.factoryMax - environment.FactoryCount);
            int want = Mathf.Min(factoryStep, room);
            if (want <= 0)
            {
                Debug.Log("[DesertEnvironmentUI] Factory count already at max.");
                return;
            }

            int built = 0;
            int clearedTreesTotal = 0;
            for (int i = 0; i < want; i++)
            {
                if (!factoryPlacement.BuildFactory(out int cleared))
                    break;
                built++;
                clearedTreesTotal += cleared;
            }

            if (built > 0)
            {
                environment.AddFactories(built);
                // 覆盖树木时：世界树已删，同步 UI 树数量
                SyncTreeCountFromWorld();
            }
            else
            {
                Debug.LogWarning("[DesertEnvironmentUI] No space to build factory.");
            }

            if (clearedTreesTotal > 0)
                Debug.Log($"[DesertEnvironmentUI] Factory cleared {clearedTreesTotal} tree(s).");

            RefreshLabels();
        }

        /// <summary>UI：拆除厂房 (-)</summary>
        public void OnRemoveFactoryClicked()
        {
            if (!EnsureSystem()) return;
            ResolveRefs();

            if (factoryPlacement == null) return;

            int removed = 0;
            for (int i = 0; i < factoryStep; i++)
            {
                if (!factoryPlacement.RemoveLastFactory()) break;
                removed++;
            }

            if (removed > 0)
                environment.AddFactories(-removed);
            RefreshLabels();
        }

        /// <summary>UI：开采 (+) — 圈定范围删树，并增加开采计数</summary>
        public void OnStartMiningClicked()
        {
            if (!EnsureSystem()) return;
            ResolveRefs();

            if (miningSystem == null)
            {
                Debug.LogWarning("[DesertEnvironmentUI] MiningSystem missing. Run Sand / Setup World Placement.");
                return;
            }

            for (int i = 0; i < miningStep; i++)
            {
                int cleared = miningSystem.MineOnce(out _, out _);
                environment.AddMining(1);
                SyncTreeCountFromWorld();
                if (cleared > 0)
                    Debug.Log($"[DesertEnvironmentUI] Mining cleared {cleared} tree(s).");
            }

            RefreshLabels();
        }

        /// <summary>UI：减少开采计数（不恢复树木）</summary>
        public void OnReduceMiningClicked()
        {
            if (!EnsureSystem()) return;
            environment.AddMining(-miningStep);
            RefreshLabels();
        }

        /// <summary>UI：是否自动根据环境改风向</summary>
        public void OnAutoWindDirectionChanged(bool enabled)
        {
            if (!EnsureSystem()) return;
            environment.EnableAutoWindDirection = enabled;
            RefreshLabels();
        }

        /// <summary>以场景里真实存活树数量为准，同步环境指数用的树计数。</summary>
        private void SyncTreeCountFromWorld()
        {
            if (environment == null) return;
            if (treePlacement == null)
            {
                treePlacement = TreePlacementSystem.Instance;
                if (treePlacement == null) return;
            }
            environment.SetTreeCount(treePlacement.AliveCount);
        }

        // =====================================================================

        public void RefreshLabels()
        {
            if (environment == null) return;

            int tMax = Mathf.Max(1, environment.Config.treeMax);
            int fMax = Mathf.Max(1, environment.Config.factoryMax);
            int mMax = Mathf.Max(1, environment.Config.miningMax);

            if (treeLabel != null)
                treeLabel.text = $"树木  {environment.TreeCount} / {tMax}";
            if (factoryLabel != null)
                factoryLabel.text = $"工厂  {environment.FactoryCount} / {fMax}";
            if (miningLabel != null)
                miningLabel.text = $"开采  {environment.MiningCount} / {mMax}";
            if (environmentLabel != null)
                environmentLabel.text = $"环境指数 E  {environment.EnvironmentIndex:0.00}";
            if (windDirHintLabel != null)
            {
                windDirHintLabel.text = environment.EnableAutoWindDirection
                    ? "自动风向：开（E 低 → 偏 Bad 风向）"
                    : "自动风向：关（保持当前风向）";
            }
        }

        private bool EnsureSystem()
        {
            if (environment != null) return true;
            environment = DesertEnvironmentSystem.Instance != null
                ? DesertEnvironmentSystem.Instance
                : FindFirstObjectByType<DesertEnvironmentSystem>();
            if (environment == null)
            {
                Debug.LogWarning("[DesertEnvironmentUI] DesertEnvironmentSystem missing. Run Sand / Setup Environment Decision UI.");
                return false;
            }
            return true;
        }
    }
}
