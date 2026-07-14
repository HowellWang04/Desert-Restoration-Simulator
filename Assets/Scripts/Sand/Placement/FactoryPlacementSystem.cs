using System.Collections.Generic;
using UnityEngine;

namespace SandSystem
{
    /// <summary>
    /// 建厂：集群分布，不叠工厂；优先避开树，无空位时可占树位并删树（同步 UI 树数量）。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sand/Factory Placement System")]
    public class FactoryPlacementSystem : MonoBehaviour
    {
        public static FactoryPlacementSystem Instance { get; private set; }

        [System.Serializable]
        public class FactoryInstance
        {
            public GameObject go;
            public Vector3 position;
            public List<Vector2Int> footprint = new List<Vector2Int>();
        }

        [SerializeField] private DesertPlacementWorld world;
        [SerializeField] private TreePlacementSystem trees;
        [SerializeField] private Transform factoryRoot;
        [Tooltip("工厂集群半径（相对集群中心）")]
        [SerializeField] private float clusterRadius = 45f;
        [Tooltip("相对已有工厂的抖动半径")]
        [SerializeField] private float clusterJitter = 18f;
        [SerializeField] private int placeAttempts = 60;
        [SerializeField] private int placeAttemptsAllowClearTrees = 40;

        private readonly List<FactoryInstance> _factories = new List<FactoryInstance>(32);
        private readonly List<Vector2Int> _tmpFootprint = new List<Vector2Int>(64);
        private Vector3 _clusterCenter;
        private bool _hasCluster;

        public int AliveCount => _factories.Count;
        public IReadOnlyList<FactoryInstance> Factories => _factories;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            if (world == null) world = GetComponent<DesertPlacementWorld>() ?? DesertPlacementWorld.Instance;
            if (trees == null) trees = GetComponent<TreePlacementSystem>() ?? TreePlacementSystem.Instance;
            if (factoryRoot == null)
            {
                var root = new GameObject("Factories");
                root.transform.SetParent(transform, false);
                factoryRoot = root.transform;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// 建一座厂。返回是否成功；out clearedTrees = 被覆盖删除的树数量。
        /// </summary>
        public bool BuildFactory(out int clearedTrees)
        {
            clearedTrees = 0;
            if (world == null) world = DesertPlacementWorld.Instance;
            if (trees == null) trees = TreePlacementSystem.Instance;
            if (world == null) return false;

            var prefabs = world.FactoryPrefabs;
            if (prefabs == null || prefabs.Length == 0) return false;

            // 1) 优先：不压树、不压厂
            if (TryPickPosition(allowClearTrees: false, out var pos))
            {
                SpawnAt(pos, prefabs, out clearedTrees);
                return true;
            }

            // 2) 没空位：允许压树
            if (TryPickPosition(allowClearTrees: true, out pos))
            {
                SpawnAt(pos, prefabs, out clearedTrees);
                return true;
            }

            return false;
        }

        public bool RemoveLastFactory()
        {
            if (_factories.Count == 0) return false;
            DestroyFactoryAtIndex(_factories.Count - 1);
            if (_factories.Count == 0) _hasCluster = false;
            return true;
        }

        private void SpawnAt(Vector3 pos, GameObject[] prefabs, out int clearedTrees)
        {
            clearedTrees = 0;
            world.CollectFactoryFootprint(pos, _tmpFootprint);

            if (trees != null)
                clearedTrees = trees.RemoveTreesInFootprint(_tmpFootprint);

            // 脚印内全部标为工厂（树格已释放）
            world.MarkFootprint(_tmpFootprint, DesertPlacementWorld.Occupant.Factory);

            var prefab = prefabs[Random.Range(0, prefabs.Length)];
            var go = Instantiate(prefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), factoryRoot);

            var inst = new FactoryInstance
            {
                go = go,
                position = pos,
                footprint = new List<Vector2Int>(_tmpFootprint)
            };
            _factories.Add(inst);

            if (!_hasCluster)
            {
                _clusterCenter = pos;
                _hasCluster = true;
            }
            else
            {
                // 集群中心缓慢跟新厂，保持片区感
                _clusterCenter = Vector3.Lerp(_clusterCenter, pos, 0.15f);
            }
        }

        private bool TryPickPosition(bool allowClearTrees, out Vector3 pos)
        {
            pos = default;
            int attempts = allowClearTrees ? placeAttemptsAllowClearTrees : placeAttempts;

            for (int i = 0; i < attempts; i++)
            {
                Vector3 candidate = SampleClusterCandidate();
                if (!world.IsInsideBounds(candidate))
                    candidate = world.ClampToBounds(candidate);

                world.CollectFactoryFootprint(candidate, _tmpFootprint);
                if (_tmpFootprint.Count == 0) continue;
                if (world.FootprintHasFactory(_tmpFootprint)) continue;

                bool hasTree = world.FootprintHasTree(_tmpFootprint);
                if (hasTree && !allowClearTrees) continue;

                // 额外：非清树模式下，避开半径内尽量不要贴树太近（脚印已覆盖主判定）
                if (!allowClearTrees && hasTree) continue;

                pos = candidate;
                return true;
            }

            return false;
        }

        private Vector3 SampleClusterCandidate()
        {
            if (!_hasCluster || _factories.Count == 0)
                return world.RandomPointInBounds();

            // 在集群中心附近，或贴着一座已有工厂附近采样 → 片区聚集
            if (Random.value < 0.55f)
            {
                Vector2 offset = Random.insideUnitCircle * clusterRadius;
                return world.SampleGround(_clusterCenter.x + offset.x, _clusterCenter.z + offset.y);
            }

            var anchor = _factories[Random.Range(0, _factories.Count)].position;
            Vector2 jitter = Random.insideUnitCircle * clusterJitter;
            return world.SampleGround(anchor.x + jitter.x, anchor.z + jitter.y);
        }

        private void DestroyFactoryAtIndex(int index)
        {
            if (index < 0 || index >= _factories.Count) return;
            var inst = _factories[index];
            if (world != null && inst.footprint != null)
            {
                for (int i = 0; i < inst.footprint.Count; i++)
                {
                    if (world.GetOccupant(inst.footprint[i]) == DesertPlacementWorld.Occupant.Factory)
                        world.FreeCell(inst.footprint[i]);
                }
            }
            if (inst.go != null) Destroy(inst.go);
            _factories.RemoveAt(index);
        }
    }
}
