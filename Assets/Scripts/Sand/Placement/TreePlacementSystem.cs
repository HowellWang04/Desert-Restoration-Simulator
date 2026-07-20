using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SandSystem
{
    /// <summary>
    /// 种树：随机位置 + 随机 prefab，格子不重复，生长缩放到 0.2~1。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sand/Tree Placement System")]
    public class TreePlacementSystem : MonoBehaviour
    {
        public static TreePlacementSystem Instance { get; private set; }

        [System.Serializable]
        public class TreeInstance
        {
            public GameObject go;
            public Vector2Int cell;
            public float targetScale;
        }

        [SerializeField] private DesertPlacementWorld world;
        [SerializeField] private Transform treeRoot;
        [SerializeField] private float growDurationMin = 0.6f;
        [SerializeField] private float growDurationMax = 1.4f;
        [SerializeField] private float minScale = 0.2f;
        [SerializeField] private float maxScale = 1f;

        private readonly List<TreeInstance> _trees = new List<TreeInstance>(1024);
        private readonly Dictionary<long, int> _cellToIndex = new Dictionary<long, int>(1024);
        private readonly List<Vector2Int> _freeScratch = new List<Vector2Int>(8192);

        public int AliveCount => _trees.Count;
        public IReadOnlyList<TreeInstance> Trees => _trees;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            if (world == null) world = GetComponent<DesertPlacementWorld>() ?? DesertPlacementWorld.Instance;
            if (treeRoot == null)
            {
                var root = new GameObject("Trees");
                root.transform.SetParent(transform, false);
                treeRoot = root.transform;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>种 count 棵树，返回实际种成数量。</summary>
        public int PlantTrees(int count)
        {
            if (world == null) world = DesertPlacementWorld.Instance;
            if (world == null || count <= 0) return 0;

            var prefabs = world.TreePrefabs;
            if (prefabs == null || prefabs.Length == 0) return 0;

            // 先收集空位再洗牌：避免格子变密后「随机试 40 次失败 → 整批中断只剩 1 棵」
            CollectFreeCellsShuffled(_freeScratch);
            if (_freeScratch.Count == 0) return 0;

            int planted = 0;
            int want = Mathf.Min(count, _freeScratch.Count);
            for (int i = 0; i < want; i++)
            {
                Vector2Int cell = _freeScratch[i];
                if (!world.TryReserveTreeCell(cell))
                    continue;

                var prefab = prefabs[Random.Range(0, prefabs.Length)];
                Vector3 pos = world.TreeCellToWorldCenter(cell);
                pos = world.SampleGround(pos.x, pos.z);
                var go = Instantiate(prefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), treeRoot);
                float target = Random.Range(minScale, maxScale);
                go.transform.localScale = Vector3.zero;

                var inst = new TreeInstance { go = go, cell = cell, targetScale = target };
                _cellToIndex[DesertPlacementWorld.CellKey(cell)] = _trees.Count;
                _trees.Add(inst);
                StartCoroutine(GrowRoutine(go.transform, target));
                planted++;
            }

            return planted;
        }

        /// <summary>直接删除最近种的 / 列表末尾的 count 棵。</summary>
        public int RemoveTrees(int count)
        {
            if (count <= 0 || _trees.Count == 0) return 0;
            int remove = Mathf.Min(count, _trees.Count);
            for (int i = 0; i < remove; i++)
                DestroyTreeAtIndex(_trees.Count - 1);
            return remove;
        }

        /// <summary>删除圆形范围内的树，返回删除数量。</summary>
        public int RemoveTreesInRadius(Vector3 center, float radius)
        {
            if (radius <= 0f || _trees.Count == 0) return 0;
            float r2 = radius * radius;
            int removed = 0;
            for (int i = _trees.Count - 1; i >= 0; i--)
            {
                var t = _trees[i];
                if (t.go == null)
                {
                    DestroyTreeAtIndex(i);
                    removed++;
                    continue;
                }
                Vector3 p = t.go.transform.position;
                float dx = p.x - center.x;
                float dz = p.z - center.z;
                if (dx * dx + dz * dz <= r2)
                {
                    DestroyTreeAtIndex(i);
                    removed++;
                }
            }
            return removed;
        }

        /// <summary>删除脚印格子上的树。</summary>
        public int RemoveTreesInFootprint(List<Vector2Int> footprint)
        {
            if (footprint == null || footprint.Count == 0) return 0;
            int removed = 0;
            for (int i = 0; i < footprint.Count; i++)
            {
                long key = DesertPlacementWorld.CellKey(footprint[i]);
                if (!_cellToIndex.TryGetValue(key, out int idx)) continue;
                DestroyTreeAtIndex(idx);
                removed++;
            }
            return removed;
        }

        public bool HasTreeAtCell(Vector2Int cell)
        {
            return _cellToIndex.ContainsKey(DesertPlacementWorld.CellKey(cell));
        }

        /// <summary>当前地图还能种多少棵（空格子数）。</summary>
        public int CountFreeCells()
        {
            CollectFreeCellsShuffled(_freeScratch);
            return _freeScratch.Count;
        }

        private void CollectFreeCellsShuffled(List<Vector2Int> into)
        {
            into.Clear();
            if (world == null) return;

            var b = world.XzBounds;
            float s = world.TreeCellSize;
            int minX = Mathf.FloorToInt(b.min.x / s);
            int maxX = Mathf.FloorToInt(b.max.x / s);
            int minZ = Mathf.FloorToInt(b.min.z / s);
            int maxZ = Mathf.FloorToInt(b.max.z / s);
            if (maxX < minX || maxZ < minZ) return;

            for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
            {
                var cell = new Vector2Int(x, z);
                if (world.IsTreeCellFree(cell))
                    into.Add(cell);
            }

            // Fisher–Yates 洗牌，保持随机分布
            for (int i = into.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (into[i], into[j]) = (into[j], into[i]);
            }
        }

        private void DestroyTreeAtIndex(int index)
        {
            if (index < 0 || index >= _trees.Count) return;
            var inst = _trees[index];
            long key = DesertPlacementWorld.CellKey(inst.cell);
            _cellToIndex.Remove(key);
            if (world != null) world.FreeCell(inst.cell);
            if (inst.go != null) Destroy(inst.go);

            int last = _trees.Count - 1;
            if (index != last)
            {
                var moved = _trees[last];
                _trees[index] = moved;
                _cellToIndex[DesertPlacementWorld.CellKey(moved.cell)] = index;
            }
            _trees.RemoveAt(last);
        }

        private IEnumerator GrowRoutine(Transform t, float targetScale)
        {
            if (t == null) yield break;
            float duration = Random.Range(growDurationMin, growDurationMax);
            float elapsed = 0f;
            while (elapsed < duration && t != null)
            {
                elapsed += Time.deltaTime;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                float s = Mathf.Lerp(0f, targetScale, u);
                t.localScale = new Vector3(s, s, s);
                yield return null;
            }
            if (t != null)
                t.localScale = new Vector3(targetScale, targetScale, targetScale);
        }
    }
}
