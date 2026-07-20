using UnityEngine;

namespace SandSystem
{
    /// <summary>
    /// 金钱系统数值。enableEconomy=false 时不计费、工厂不产钱；
    /// 工厂污染杀树始终生效（与金钱开关无关）。
    /// </summary>
    [System.Serializable]
    public class DesertEconomyConfig
    {
        [Header("Master Switch")]
        [Tooltip("关闭后种树/建厂/采矿不扣钱，工厂也不产钱（污染仍会杀树）")]
        public bool enableEconomy = true;

        [Header("Starting")]
        [Tooltip("开局资金")]
        public int startingMoney = 8000;

        [Header("Tree")]
        [Tooltip("每棵树费用（按实际种成数量扣）")]
        public int treeCostPerTree = 2;

        [Header("Factory — Cost & Income")]
        [Tooltip("建一座厂的费用")]
        public int factoryBuildCost = 1200;
        [Tooltip("第 1 座厂的每秒收入（建议偏小，避免秒级暴富）")]
        public float factoryIncomePerSecond = 1.5f;
        [Tooltip("每多一座厂，该座相对基础收入线性衰减")]
        [Range(0f, 0.5f)] public float factoryIncomeDecayPerExtra = 0.18f;
        [Tooltip("单厂收入下限")]
        public float factoryIncomeMinPerFactory = 0.4f;
        [Tooltip("全图工厂收入软顶（每秒），0 = 不限制")]
        public float factoryIncomeSoftCapPerSecond = 6f;

        [Header("Factory — Pollution (持续，杀树)")]
        [Tooltip("每座厂每秒杀死的树木数（偏小更合理）")]
        public float pollutionTreesKilledPerFactoryPerSecond = 0.6f;
        [Tooltip("污染结算间隔（秒），越大节奏越慢）")]
        public float pollutionTickInterval = 2.5f;

        [Header("Mining (阶段收入，非持续)")]
        [Tooltip("每次采矿行动费用")]
        public int miningActionCost = 450;
        [Tooltip("每次采矿总阶段收入（分若干段发放，不是一直产钱）")]
        public int miningStageTotalPayout = 900;
        [Tooltip("阶段次数")]
        public int miningStageCount = 3;
        [Tooltip("阶段间隔秒")]
        public float miningStageInterval = 2.5f;
    }
}
