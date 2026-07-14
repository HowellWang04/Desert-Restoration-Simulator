using UnityEngine;

namespace SandSystem
{
    /// <summary>
    /// 采矿：在场内随机圈定范围，删除圈内树木，并增加开采计数。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sand/Mining System")]
    public class MiningSystem : MonoBehaviour
    {
        public static MiningSystem Instance { get; private set; }

        [SerializeField] private DesertPlacementWorld world;
        [SerializeField] private TreePlacementSystem trees;
        [SerializeField] private float mineRadiusMin = 12f;
        [SerializeField] private float mineRadiusMax = 28f;
        [Tooltip("调试：画出最近一次采矿圈")]
        [SerializeField] private bool drawGizmo = true;

        private Vector3 _lastCenter;
        private float _lastRadius;
        private bool _hasLast;

        public Vector3 LastMineCenter => _lastCenter;
        public float LastMineRadius => _lastRadius;

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
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>执行一次采矿。返回删除的树数量。</summary>
        public int MineOnce(out Vector3 center, out float radius)
        {
            center = default;
            radius = 0f;
            if (world == null) world = DesertPlacementWorld.Instance;
            if (trees == null) trees = TreePlacementSystem.Instance;
            if (world == null || trees == null) return 0;

            center = world.RandomPointInBounds();
            radius = Random.Range(mineRadiusMin, mineRadiusMax);
            _lastCenter = center;
            _lastRadius = radius;
            _hasLast = true;

            return trees.RemoveTreesInRadius(center, radius);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!drawGizmo || !_hasLast) return;
            Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.35f);
            Gizmos.DrawSphere(_lastCenter, _lastRadius);
            Gizmos.color = new Color(1f, 0.5f, 0.1f, 1f);
            Gizmos.DrawWireSphere(_lastCenter, _lastRadius);
        }
#endif
    }
}
