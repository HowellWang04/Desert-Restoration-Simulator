using UnityEngine;
using UnityEngine.UI;

namespace SandSystem
{
    /// <summary>
    /// 三项决策 UI：种树 / 建厂 / 开采 + 金钱显示。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sand/Desert Environment UI")]
    public class DesertEnvironmentUI : MonoBehaviour
    {
        [Header("System")]
        [SerializeField] private DesertEnvironmentSystem environment;
        [SerializeField] private DesertEconomySystem economy;
        [SerializeField] private TreePlacementSystem treePlacement;
        [SerializeField] private FactoryPlacementSystem factoryPlacement;
        [SerializeField] private MiningSystem miningSystem;

        [Header("Labels")]
        [SerializeField] private Text moneyLabel;
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

        [Header("Options")]
        [SerializeField] private Toggle autoWindDirectionToggle;
        [SerializeField] private Toggle enableEconomyToggle;

        [Header("Step")]
        [SerializeField] private int treeStep = 100;
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
            if (economy != null)
                economy.OnMoneyChanged += RefreshLabels;
            RefreshLabels();
            SyncTogglesFromSystem();
        }

        private void OnDisable()
        {
            if (environment != null)
                environment.OnEnvironmentChanged -= RefreshLabels;
            if (economy != null)
                economy.OnMoneyChanged -= RefreshLabels;
        }

        private void ResolveRefs()
        {
            if (environment == null)
                environment = DesertEnvironmentSystem.Instance != null
                    ? DesertEnvironmentSystem.Instance
                    : FindFirstObjectByType<DesertEnvironmentSystem>();

            if (economy == null)
                economy = DesertEconomySystem.Instance != null
                    ? DesertEconomySystem.Instance
                    : FindFirstObjectByType<DesertEconomySystem>();

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

            if (autoWindDirectionToggle != null)
            {
                autoWindDirectionToggle.onValueChanged.RemoveAllListeners();
                autoWindDirectionToggle.onValueChanged.AddListener(OnAutoWindDirectionChanged);
            }

            if (enableEconomyToggle != null)
            {
                enableEconomyToggle.onValueChanged.RemoveAllListeners();
                enableEconomyToggle.onValueChanged.AddListener(OnEnableEconomyChanged);
            }
        }

        private void SyncTogglesFromSystem()
        {
            if (environment != null && autoWindDirectionToggle != null)
                autoWindDirectionToggle.SetIsOnWithoutNotify(environment.EnableAutoWindDirection);
            if (economy != null && enableEconomyToggle != null)
                enableEconomyToggle.SetIsOnWithoutNotify(economy.EnableEconomy);
        }

        // =====================================================================

        public void OnPlantTreeClicked()
        {
            if (!EnsureSystem()) return;
            ResolveRefs();
            if (treePlacement == null) return;

            int want = treeStep;
            if (economy != null && economy.EnableEconomy)
            {
                want = economy.MaxAffordableTrees(treeStep);
                if (want <= 0)
                {
                    Debug.Log("[DesertEnvironmentUI] 金钱不足，无法种树。");
                    RefreshLabels();
                    return;
                }
            }

            int planted = treePlacement.PlantTrees(want);
            if (planted > 0 && economy != null)
                economy.TryPayForTrees(planted);

            SyncTreeCountFromWorld();
            if (planted < treeStep)
                Debug.Log($"[DesertEnvironmentUI] Planted {planted}/{treeStep} (资金或空位不足).");
            RefreshLabels();
        }

        public void OnRemoveTreeClicked()
        {
            if (!EnsureSystem()) return;
            ResolveRefs();
            if (treePlacement == null) return;

            treePlacement.RemoveTrees(treeStep);
            SyncTreeCountFromWorld();
            RefreshLabels();
        }

        public void OnBuildFactoryClicked()
        {
            if (!EnsureSystem()) return;
            ResolveRefs();
            if (factoryPlacement == null) return;

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
                if (economy != null && economy.EnableEconomy && !economy.CanAffordFactory())
                {
                    if (built == 0)
                        Debug.Log("[DesertEnvironmentUI] 金钱不足，无法建厂。");
                    break;
                }

                if (!factoryPlacement.BuildFactory(out int cleared))
                    break;

                if (economy != null && !economy.TryPayForFactory())
                {
                    // 理论上 CanAfford 已过；若失败则拆除刚建的（极少见）
                    factoryPlacement.RemoveLastFactory();
                    break;
                }

                built++;
                clearedTreesTotal += cleared;
            }

            if (built > 0)
            {
                environment.AddFactories(built);
                SyncTreeCountFromWorld();
            }

            if (clearedTreesTotal > 0)
                Debug.Log($"[DesertEnvironmentUI] Factory cleared {clearedTreesTotal} tree(s).");

            RefreshLabels();
        }

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

        public void OnStartMiningClicked()
        {
            if (!EnsureSystem()) return;
            ResolveRefs();
            if (miningSystem == null) return;

            for (int i = 0; i < miningStep; i++)
            {
                if (economy != null && economy.EnableEconomy && !economy.CanAffordMining())
                {
                    if (i == 0)
                        Debug.Log("[DesertEnvironmentUI] 金钱不足，无法采矿。");
                    break;
                }

                if (economy != null && !economy.TryPayForMiningAndSchedulePayout())
                    break;

                int cleared = miningSystem.MineOnce(out _, out _);
                environment.AddMining(1);
                SyncTreeCountFromWorld();
                if (cleared > 0)
                    Debug.Log($"[DesertEnvironmentUI] Mining cleared {cleared} tree(s).");
            }

            RefreshLabels();
        }

        public void OnAutoWindDirectionChanged(bool enabled)
        {
            if (!EnsureSystem()) return;
            environment.EnableAutoWindDirection = enabled;
            RefreshLabels();
        }

        public void OnEnableEconomyChanged(bool enabled)
        {
            ResolveRefs();
            if (economy == null) return;
            economy.EnableEconomy = enabled;
            RefreshLabels();
        }

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

        public void RefreshLabels()
        {
            if (environment == null) return;

            int fMax = Mathf.Max(1, environment.Config.factoryMax);
            int mMax = Mathf.Max(1, environment.Config.miningMax);

            if (moneyLabel != null)
            {
                if (economy == null || !economy.EnableEconomy)
                    moneyLabel.text = "金钱  （系统关闭）";
                else
                    moneyLabel.text = $"金钱  {economy.Money}";
            }

            if (treeLabel != null)
            {
                string costHint = "";
                if (economy != null && economy.EnableEconomy)
                    costHint = $"  ·种{treeStep}棵需{economy.TreeBatchCost(treeStep)}";
                treeLabel.text = $"树木  {environment.TreeCount}{costHint}";
            }

            if (factoryLabel != null)
            {
                string costHint = "";
                if (economy != null)
                {
                    float income = economy.CurrentFactoryIncomePerSecond;
                    float kill = economy.CurrentPollutionKillPerSecond;
                    if (economy.EnableEconomy)
                        costHint = $"  ·建{economy.FactoryCost} / +{income:0.#}/秒 / 污染-{kill:0.#}树/秒";
                    else if (kill > 0.01f)
                        costHint = $"  ·污染-{kill:0.#}树/秒";
                }
                factoryLabel.text = $"工厂  {environment.FactoryCount} / {fMax}{costHint}";
            }

            if (miningLabel != null)
            {
                string costHint = "";
                if (economy != null && economy.EnableEconomy)
                    costHint = $"  ·费{economy.MiningCost}→收{economy.Config.miningStageTotalPayout}";
                miningLabel.text = $"开采  {environment.MiningCount} / {mMax}{costHint}";
            }

            if (environmentLabel != null)
            {
                float clear = environment.EvaluateClearSkyBlend();
                string skyHint = clear >= 0.999f
                    ? "  · 晴空"
                    : clear > 0.01f
                        ? $"  · 晴空过渡 {clear:0%}"
                        : "";
                environmentLabel.text = $"环境指数 E  {environment.EnvironmentIndex:0.00}{skyHint}";
            }

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
                Debug.LogWarning("[DesertEnvironmentUI] DesertEnvironmentSystem missing.");
                return false;
            }
            return true;
        }
    }
}
