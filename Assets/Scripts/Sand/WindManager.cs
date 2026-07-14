using UnityEngine;

namespace SandSystem
{
    /// <summary>
    /// 全局风向 / 风速。只推送 Shader Global，滚动位移由 GPU 时间完成（更流畅）。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class WindManager : MonoBehaviour
    {
        public static WindManager Instance { get; private set; }

        static readonly int SandGlobalWindId = Shader.PropertyToID("_SandGlobalWind");

        [Header("Wind")]
        [SerializeField] private Vector2 windDirection = new Vector2(1f, 0.25f);
        [SerializeField, Range(0f, 5f)] private float windSpeed = 1.25f;

        [Header("Optional Gust")]
        [SerializeField] private bool enableGust = false;
        [SerializeField, Range(0f, 1f)] private float gustStrength = 0.15f;
        [SerializeField, Range(0.01f, 2f)] private float gustFrequency = 0.2f;

        public Vector2 WindDirection => windDirection.sqrMagnitude > 1e-6f
            ? windDirection.normalized
            : Vector2.right;

        /// <summary>配置风速（不含阵风抖动），供外部系统读写。</summary>
        public float BaseWindSpeed
        {
            get => windSpeed;
            set => SetWindSpeed(value);
        }

        /// <summary>当前生效风速（含可选阵风）。</summary>
        public float WindSpeed
        {
            get
            {
                if (!enableGust)
                    return windSpeed;

                float gust = 1f + Mathf.Sin(Time.time * gustFrequency * Mathf.PI * 2f) * gustStrength;
                return Mathf.Max(0f, windSpeed * gust);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[WindManager] Duplicate instance destroyed.", this);
                Destroy(this);
                return;
            }

            Instance = this;
            PushGlobals();
        }

        private void OnEnable() => PushGlobals();

        private void LateUpdate() => PushGlobals();

        private void OnDisable()
        {
            Shader.SetGlobalVector(SandGlobalWindId, Vector4.zero);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void SetWind(Vector2 direction, float speed)
        {
            windDirection = direction;
            windSpeed = Mathf.Max(0f, speed);
            PushGlobals();
        }

        public void SetWindDirection(Vector2 direction)
        {
            windDirection = direction;
            PushGlobals();
        }

        public void SetWindSpeed(float speed)
        {
            windSpeed = Mathf.Max(0f, speed);
            PushGlobals();
        }

        void PushGlobals()
        {
            Vector2 dir = WindDirection;
            Shader.SetGlobalVector(SandGlobalWindId, new Vector4(dir.x, dir.y, WindSpeed, 1f));
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            Vector3 dir = new Vector3(WindDirection.x, 0f, WindDirection.y);
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(origin, dir * (1f + WindSpeed));
            Gizmos.DrawWireSphere(origin + dir * (1f + WindSpeed), 0.08f);
        }

        private void OnValidate()
        {
            if (isActiveAndEnabled)
                PushGlobals();
        }
#endif
    }
}
