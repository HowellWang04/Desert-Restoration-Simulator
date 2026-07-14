using System.Collections.Generic;
using UnityEngine;

namespace SandSystem
{
    /// <summary>
    /// 世界放置公共配置：范围、地形高度、格子占用。
    /// 与 Tree / Factory / Mining 放在同一物体上（建议名 WorldPlacement）。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sand/World Placement")]
    public class DesertPlacementWorld : MonoBehaviour
    {
        public static DesertPlacementWorld Instance { get; private set; }

        public enum Occupant : byte
        {
            Empty = 0,
            Tree = 1,
            Factory = 2
        }

        [Header("Play Area")]
        [Tooltip("为空则自动用 Terrain 范围；无 Terrain 则用下方手动范围")]
        [SerializeField] private Terrain terrain;
        [SerializeField] private Vector3 manualCenter = Vector3.zero;
        [SerializeField] private Vector2 manualSize = new Vector2(200f, 200f);
        [SerializeField] private float yOffset = 0.02f;

        [Header("Grid")]
        [Tooltip("树木占用格子边长（米）")]
        [SerializeField] private float treeCellSize = 2f;
        [Tooltip("工厂占用半径（米），互相不重叠）")]
        [SerializeField] private float factoryRadius = 8f;
        [Tooltip("工厂尽量避开树木时的检测半径")]
        [SerializeField] private float factoryTreeAvoidRadius = 6f;

        [Header("Prefabs (Resources)")]
        [SerializeField] private string treeResourcePath = "_Prefab/tree";
        [SerializeField] private string factoryResourcePath = "_Prefab/factory";

        private readonly Dictionary<long, Occupant> _cells = new Dictionary<long, Occupant>(4096);
        private GameObject[] _treePrefabs;
        private GameObject[] _factoryPrefabs;
        private Bounds _xzBounds;
        private bool _boundsReady;

        public float TreeCellSize => Mathf.Max(0.25f, treeCellSize);
        public float FactoryRadius => Mathf.Max(1f, factoryRadius);
        public float FactoryTreeAvoidRadius => Mathf.Max(0.5f, factoryTreeAvoidRadius);
        public Bounds XzBounds
        {
            get
            {
                EnsureBounds();
                return _xzBounds;
            }
        }

        public GameObject[] TreePrefabs
        {
            get
            {
                EnsurePrefabs();
                return _treePrefabs;
            }
        }

        public GameObject[] FactoryPrefabs
        {
            get
            {
                EnsurePrefabs();
                return _factoryPrefabs;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[DesertPlacementWorld] Duplicate, destroying this.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            EnsureBounds();
            EnsurePrefabs();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void EnsureBounds()
        {
            if (_boundsReady && terrain == null) return;

            if (terrain == null)
                terrain = Terrain.activeTerrain;

            if (terrain != null)
            {
                var td = terrain.terrainData;
                var pos = terrain.transform.position;
                _xzBounds = new Bounds(
                    new Vector3(pos.x + td.size.x * 0.5f, pos.y, pos.z + td.size.z * 0.5f),
                    new Vector3(td.size.x, td.size.y, td.size.z));
            }
            else
            {
                _xzBounds = new Bounds(
                    new Vector3(manualCenter.x, 0f, manualCenter.z),
                    new Vector3(manualSize.x, 100f, manualSize.y));
            }

            _boundsReady = true;
        }

        public void EnsurePrefabs()
        {
            if (_treePrefabs == null || _treePrefabs.Length == 0)
                _treePrefabs = Resources.LoadAll<GameObject>(treeResourcePath);
            if (_factoryPrefabs == null || _factoryPrefabs.Length == 0)
                _factoryPrefabs = Resources.LoadAll<GameObject>(factoryResourcePath);

            if (_treePrefabs == null || _treePrefabs.Length == 0)
                Debug.LogWarning($"[DesertPlacementWorld] No tree prefabs at Resources/{treeResourcePath}");
            if (_factoryPrefabs == null || _factoryPrefabs.Length == 0)
                Debug.LogWarning($"[DesertPlacementWorld] No factory prefabs at Resources/{factoryResourcePath}");
        }

        public Vector3 SampleGround(float x, float z)
        {
            float y = 0f;
            if (terrain != null)
                y = terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
            else if (Physics.Raycast(new Vector3(x, 5000f, z), Vector3.down, out var hit, 10000f))
                y = hit.point.y;
            return new Vector3(x, y + yOffset, z);
        }

        public Vector2Int WorldToTreeCell(Vector3 world)
        {
            float s = TreeCellSize;
            return new Vector2Int(Mathf.FloorToInt(world.x / s), Mathf.FloorToInt(world.z / s));
        }

        public Vector3 TreeCellToWorldCenter(Vector2Int cell)
        {
            float s = TreeCellSize;
            float x = (cell.x + 0.5f) * s;
            float z = (cell.y + 0.5f) * s;
            return SampleGround(x, z);
        }

        public static long CellKey(Vector2Int cell)
        {
            return ((long)cell.x << 32) ^ (uint)cell.y;
        }

        public Occupant GetOccupant(Vector2Int cell)
        {
            return _cells.TryGetValue(CellKey(cell), out var o) ? o : Occupant.Empty;
        }

        public bool IsTreeCellFree(Vector2Int cell)
        {
            return GetOccupant(cell) == Occupant.Empty;
        }

        public void SetOccupant(Vector2Int cell, Occupant occupant)
        {
            long key = CellKey(cell);
            if (occupant == Occupant.Empty)
                _cells.Remove(key);
            else
                _cells[key] = occupant;
        }

        public bool TryReserveTreeCell(Vector2Int cell)
        {
            if (!IsTreeCellFree(cell)) return false;
            SetOccupant(cell, Occupant.Tree);
            return true;
        }

        public void FreeCell(Vector2Int cell)
        {
            SetOccupant(cell, Occupant.Empty);
        }

        /// <summary>工厂脚印格子（圆形近似）。</summary>
        public void CollectFactoryFootprint(Vector3 center, List<Vector2Int> into)
        {
            into.Clear();
            float r = FactoryRadius;
            float s = TreeCellSize;
            int minX = Mathf.FloorToInt((center.x - r) / s);
            int maxX = Mathf.FloorToInt((center.x + r) / s);
            int minZ = Mathf.FloorToInt((center.z - r) / s);
            int maxZ = Mathf.FloorToInt((center.z + r) / s);
            float r2 = r * r;
            for (int x = minX; x <= maxX; x++)
            for (int z = minZ; z <= maxZ; z++)
            {
                float cx = (x + 0.5f) * s;
                float cz = (z + 0.5f) * s;
                float dx = cx - center.x;
                float dz = cz - center.z;
                if (dx * dx + dz * dz <= r2)
                    into.Add(new Vector2Int(x, z));
            }
        }

        public bool FootprintHasFactory(List<Vector2Int> footprint)
        {
            for (int i = 0; i < footprint.Count; i++)
            {
                if (GetOccupant(footprint[i]) == Occupant.Factory)
                    return true;
            }
            return false;
        }

        public bool FootprintHasTree(List<Vector2Int> footprint)
        {
            for (int i = 0; i < footprint.Count; i++)
            {
                if (GetOccupant(footprint[i]) == Occupant.Tree)
                    return true;
            }
            return false;
        }

        public void MarkFootprint(List<Vector2Int> footprint, Occupant occupant)
        {
            for (int i = 0; i < footprint.Count; i++)
                SetOccupant(footprint[i], occupant);
        }

        public Vector3 RandomPointInBounds()
        {
            var b = XzBounds;
            float x = Random.Range(b.min.x, b.max.x);
            float z = Random.Range(b.min.z, b.max.z);
            return SampleGround(x, z);
        }

        public bool IsInsideBounds(Vector3 p)
        {
            var b = XzBounds;
            return p.x >= b.min.x && p.x <= b.max.x && p.z >= b.min.z && p.z <= b.max.z;
        }

        public Vector3 ClampToBounds(Vector3 p)
        {
            var b = XzBounds;
            p.x = Mathf.Clamp(p.x, b.min.x, b.max.x);
            p.z = Mathf.Clamp(p.z, b.min.z, b.max.z);
            return SampleGround(p.x, p.z);
        }
    }
}
