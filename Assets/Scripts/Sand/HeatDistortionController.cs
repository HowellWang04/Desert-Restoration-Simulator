using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.Rendering.Universal;
#endif

namespace SandSystem
{
    /// <summary>
    /// 场景面板控制热浪（与 WindManager / AirborneSand 用法相同）。
    /// 挂在场景物体上，在 Inspector 里直接调参数。
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public class HeatDistortionController : MonoBehaviour
    {
        public static HeatDistortionController Instance { get; private set; }

        [Header("Enable")]
        [SerializeField] private bool enableHeat = true;

        [Header("Distortion")]
        [SerializeField, Range(0f, 0.1f)] private float distortionStrength = 0.015f;
        [SerializeField, Min(0.1f)] private float noiseScale = 8f;
        [SerializeField, Min(0f)] private float scrollSpeed = 0.15f;
        [SerializeField, Range(0f, 1f)] private float verticalBias = 0.65f;

        [Header("Wind")]
        [Tooltip("关闭则跟随 WindManager 全局风向")]
        [SerializeField] private bool useLocalWind;
        [SerializeField] private Vector2 localWindDirection = new Vector2(1f, 0.2f);

        [Header("Depth / Height Mask")]
        [SerializeField] private bool useDepthMask = true;
        [SerializeField] private float depthMaskStart = 5f;
        [SerializeField] private float depthMaskEnd = 80f;
        [SerializeField] private float heatHeightStart = 0.5f;
        [SerializeField] private float heatHeightEnd = 12f;
        [SerializeField, Range(0.2f, 4f)] private float heatMaskPower = 1.2f;

        [Header("Advanced")]
        [SerializeField] private HeatDistortionRendererFeature.SourceMode sourceMode =
            HeatDistortionRendererFeature.SourceMode.BlitColor;
        [SerializeField] private Texture2D noiseTexture;
        [SerializeField] private Material heatMaterial;

        public bool EnableHeat
        {
            get => enableHeat;
            set { enableHeat = value; Apply(); }
        }

        public float DistortionStrength
        {
            get => distortionStrength;
            set { distortionStrength = Mathf.Clamp(value, 0f, 0.1f); Apply(); }
        }

        public float NoiseScale
        {
            get => noiseScale;
            set { noiseScale = Mathf.Max(0.1f, value); Apply(); }
        }

        public float ScrollSpeed
        {
            get => scrollSpeed;
            set { scrollSpeed = Mathf.Max(0f, value); Apply(); }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[HeatDistortionController] Duplicate destroyed.", this);
                Destroy(this);
                return;
            }

            Instance = this;
        }

        private void OnEnable()
        {
            Instance = this;
            Apply();
        }

        private void OnDisable()
        {
            if (Instance == this)
                Instance = null;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void OnValidate() => Apply();

        private void LateUpdate() => Apply();

        [ContextMenu("Apply Heat Settings")]
        public void Apply()
        {
            var feature = ResolveFeature();
            if (feature == null)
                return;

            var s = feature.settings;
            s.isEnabled = enableHeat;
            s.distortionStrength = enableHeat ? distortionStrength : 0f;
            s.noiseScale = noiseScale;
            s.scrollSpeed = scrollSpeed;
            s.verticalBias = verticalBias;
            s.useDepthMask = useDepthMask;
            s.depthMaskStart = depthMaskStart;
            s.depthMaskEnd = depthMaskEnd;
            s.heatHeightStart = heatHeightStart;
            s.heatHeightEnd = heatHeightEnd;
            s.heatMaskPower = heatMaskPower;
            s.sourceMode = sourceMode;

            if (useLocalWind)
                s.windDirection = localWindDirection.sqrMagnitude > 1e-6f
                    ? localWindDirection.normalized
                    : Vector2.right;
            else if (WindManager.Instance != null)
                s.windDirection = WindManager.Instance.WindDirection;

            if (noiseTexture != null)
                s.noiseTexture = noiseTexture;

            if (heatMaterial != null)
                s.material = heatMaterial;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                EditorUtility.SetDirty(feature);
#endif
        }

        HeatDistortionRendererFeature ResolveFeature()
        {
            if (HeatDistortionRendererFeature.Instance != null)
                return HeatDistortionRendererFeature.Instance;

#if UNITY_EDITOR
            return FindFeatureInProject();
#else
            return null;
#endif
        }

#if UNITY_EDITOR
        static HeatDistortionRendererFeature FindFeatureInProject()
        {
            string[] guids = AssetDatabase.FindAssets("t:UniversalRendererData");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (data == null)
                    continue;

                foreach (var f in data.rendererFeatures)
                {
                    if (f is HeatDistortionRendererFeature heat)
                        return heat;
                }
            }

            return null;
        }
#endif
    }
}
