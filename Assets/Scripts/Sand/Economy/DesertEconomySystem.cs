using System.Collections.Generic;
using UnityEngine;

namespace SandSystem
{
    /// <summary>
    /// 金钱：种树/建厂/采矿扣费；工厂收入（递减）；采矿阶段收入。
    /// 污染：工厂持续杀树（始终生效，逼迫终局拆厂才能晴空）。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sand/Desert Economy System")]
    public class DesertEconomySystem : MonoBehaviour
    {
        public static DesertEconomySystem Instance { get; private set; }

        [SerializeField] private DesertEconomyConfig config = new DesertEconomyConfig();
        [SerializeField] private DesertEnvironmentSystem environment;
        [SerializeField] private TreePlacementSystem trees;
        [SerializeField] private int money;

        private float _factoryIncomeAcc;
        private float _pollutionAcc;
        private float _pollutionTimer;
        private readonly List<MiningPayoutJob> _miningJobs = new List<MiningPayoutJob>(8);

        struct MiningPayoutJob
        {
            public int remainingStages;
            public int payoutPerStage;
            public float nextTime;
            public float interval;
        }

        public DesertEconomyConfig Config => config;
        public int Money => money;

        /// <summary>当前工厂总收入速率（每秒），已含递减与软顶。</summary>
        public float CurrentFactoryIncomePerSecond
        {
            get
            {
                int n = environment != null ? environment.FactoryCount : 0;
                return ComputeFactoryIncomeRate(n);
            }
        }

        /// <summary>当前污染杀树速率（棵/秒）。</summary>
        public float CurrentPollutionKillPerSecond
        {
            get
            {
                int n = environment != null ? environment.FactoryCount : 0;
                return Mathf.Max(0f, n * config.pollutionTreesKilledPerFactoryPerSecond);
            }
        }

        public bool EnableEconomy
        {
            get => config.enableEconomy;
            set
            {
                config.enableEconomy = value;
                OnMoneyChanged?.Invoke();
            }
        }

        public event System.Action OnMoneyChanged;
        public event System.Action OnPollutionTick;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            if (environment == null)
                environment = GetComponent<DesertEnvironmentSystem>() ?? DesertEnvironmentSystem.Instance;
            if (trees == null)
                trees = TreePlacementSystem.Instance ?? FindFirstObjectByType<TreePlacementSystem>();
            money = Mathf.Max(0, config.startingMoney);
        }

        private void Start()
        {
            if (money <= 0 && config.startingMoney > 0 && config.enableEconomy)
                money = config.startingMoney;
            OnMoneyChanged?.Invoke();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            // 污染始终 tick（与金钱开关无关）
            TickFactoryPollution();

            if (!config.enableEconomy) return;
            TickFactoryIncome();
            TickMiningStages();
        }

        private void TickFactoryIncome()
        {
            if (environment == null)
                environment = DesertEnvironmentSystem.Instance;
            int factories = environment != null ? environment.FactoryCount : 0;
            float rate = ComputeFactoryIncomeRate(factories);
            if (rate <= 0.0001f) return;

            _factoryIncomeAcc += rate * Time.deltaTime;
            if (_factoryIncomeAcc < 1f) return;

            int gain = Mathf.FloorToInt(_factoryIncomeAcc);
            _factoryIncomeAcc -= gain;
            money += gain;
            OnMoneyChanged?.Invoke();
        }

        /// <summary>
        /// 第 i 座厂收入 = max(min, base * (1 - i * decay))，再软顶。
        /// </summary>
        public float ComputeFactoryIncomeRate(int factoryCount)
        {
            if (factoryCount <= 0) return 0f;
            float total = 0f;
            float bas = Mathf.Max(0f, config.factoryIncomePerSecond);
            float decay = Mathf.Clamp01(config.factoryIncomeDecayPerExtra);
            float min = Mathf.Max(0f, config.factoryIncomeMinPerFactory);
            for (int i = 0; i < factoryCount; i++)
            {
                float factor = Mathf.Max(0f, 1f - i * decay);
                total += Mathf.Max(min, bas * factor);
            }

            float cap = config.factoryIncomeSoftCapPerSecond;
            if (cap > 0f)
                total = Mathf.Min(total, cap);
            return total;
        }

        private void TickFactoryPollution()
        {
            if (environment == null)
                environment = DesertEnvironmentSystem.Instance;
            if (trees == null)
                trees = TreePlacementSystem.Instance;

            int factories = environment != null ? environment.FactoryCount : 0;
            if (factories <= 0 || trees == null)
            {
                _pollutionTimer = 0f;
                return;
            }

            float killRate = factories * Mathf.Max(0f, config.pollutionTreesKilledPerFactoryPerSecond);
            if (killRate <= 0f) return;

            float interval = Mathf.Max(0.5f, config.pollutionTickInterval);
            _pollutionTimer += Time.deltaTime;
            _pollutionAcc += killRate * Time.deltaTime;
            if (_pollutionTimer < interval) return;
            _pollutionTimer = 0f;

            int toKill = Mathf.Min(Mathf.FloorToInt(_pollutionAcc), 50);
            if (toKill <= 0) return;

            int killed = trees.RemoveTrees(toKill);
            _pollutionAcc -= killed;
            if (_pollutionAcc < 0f) _pollutionAcc = 0f;

            if (killed > 0 && environment != null)
            {
                environment.SetTreeCount(trees.AliveCount);
                OnPollutionTick?.Invoke();
                OnMoneyChanged?.Invoke();
            }
        }

        private void TickMiningStages()
        {
            if (_miningJobs.Count == 0) return;
            float now = Time.time;
            bool changed = false;
            for (int i = _miningJobs.Count - 1; i >= 0; i--)
            {
                var job = _miningJobs[i];
                while (job.remainingStages > 0 && now >= job.nextTime)
                {
                    money += job.payoutPerStage;
                    job.remainingStages--;
                    job.nextTime += job.interval;
                    changed = true;
                }
                if (job.remainingStages <= 0)
                    _miningJobs.RemoveAt(i);
                else
                    _miningJobs[i] = job;
            }
            if (changed) OnMoneyChanged?.Invoke();
        }

        public void SetMoney(int value)
        {
            money = Mathf.Max(0, value);
            OnMoneyChanged?.Invoke();
        }

        public void AddMoney(int delta)
        {
            if (!config.enableEconomy || delta == 0) return;
            money = Mathf.Max(0, money + delta);
            OnMoneyChanged?.Invoke();
        }

        public int MaxAffordableTrees(int wantCount)
        {
            if (!config.enableEconomy) return wantCount;
            int cost = Mathf.Max(0, config.treeCostPerTree);
            if (cost <= 0) return wantCount;
            return Mathf.Min(wantCount, money / cost);
        }

        public bool CanAffordTrees(int count)
        {
            if (!config.enableEconomy) return true;
            return money >= Mathf.Max(0, config.treeCostPerTree) * Mathf.Max(0, count);
        }

        public bool TryPayForTrees(int plantedCount)
        {
            if (!config.enableEconomy || plantedCount <= 0) return true;
            int cost = Mathf.Max(0, config.treeCostPerTree) * plantedCount;
            if (money < cost) return false;
            money -= cost;
            OnMoneyChanged?.Invoke();
            return true;
        }

        public bool CanAffordFactory()
        {
            if (!config.enableEconomy) return true;
            return money >= Mathf.Max(0, config.factoryBuildCost);
        }

        public bool TryPayForFactory()
        {
            if (!config.enableEconomy) return true;
            int cost = Mathf.Max(0, config.factoryBuildCost);
            if (money < cost) return false;
            money -= cost;
            OnMoneyChanged?.Invoke();
            return true;
        }

        public bool CanAffordMining()
        {
            if (!config.enableEconomy) return true;
            return money >= Mathf.Max(0, config.miningActionCost);
        }

        public bool TryPayForMiningAndSchedulePayout()
        {
            if (!config.enableEconomy) return true;

            int cost = Mathf.Max(0, config.miningActionCost);
            if (money < cost) return false;
            money -= cost;
            OnMoneyChanged?.Invoke();

            int stages = Mathf.Max(1, config.miningStageCount);
            int total = Mathf.Max(0, config.miningStageTotalPayout);
            int per = total / stages;
            int rem = total - per * stages;

            _miningJobs.Add(new MiningPayoutJob
            {
                remainingStages = stages,
                payoutPerStage = per,
                nextTime = Time.time + Mathf.Max(0.05f, config.miningStageInterval),
                interval = Mathf.Max(0.05f, config.miningStageInterval)
            });

            if (rem > 0)
            {
                money += rem;
                OnMoneyChanged?.Invoke();
            }

            return true;
        }

        public int TreeBatchCost(int count) =>
            config.enableEconomy ? Mathf.Max(0, config.treeCostPerTree) * Mathf.Max(0, count) : 0;

        public int FactoryCost => config.enableEconomy ? Mathf.Max(0, config.factoryBuildCost) : 0;
        public int MiningCost => config.enableEconomy ? Mathf.Max(0, config.miningActionCost) : 0;
    }
}
