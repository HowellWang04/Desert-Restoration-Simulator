using UnityEngine;

namespace SandSystem
{
    /// <summary>
    /// 空中扬沙层：在地表上方生成多层水平沙幕，跟随全局风向流动。
    /// 依赖 WindManager 的 _SandFlowUV / _SandGlobalWind。
    /// </summary>
        [DisallowMultipleComponent]
    public class AirborneSandController : MonoBehaviour
    {
        public static AirborneSandController Instance { get; private set; }

        static readonly int SpeedScaleId = Shader.PropertyToID("_SpeedScale");
        static readonly int LayerSpeedId = Shader.PropertyToID("_LayerSpeed");
        static readonly int DensityId = Shader.PropertyToID("_Density");
        static readonly int TilingId = Shader.PropertyToID("_Tiling");
        static readonly int SoftDepthFadeId = Shader.PropertyToID("_SoftDepthFade");
        static readonly int HeightFadeId = Shader.PropertyToID("_HeightFade");
        static readonly int ColorNearId = Shader.PropertyToID("_ColorNear");
        static readonly int ColorFarId = Shader.PropertyToID("_ColorFar");
        static readonly int NoiseTexAId = Shader.PropertyToID("_NoiseTexA");
        static readonly int NoiseTexBId = Shader.PropertyToID("_NoiseTexB");

        [System.Serializable]
        public class SheetSettings
        {
            public string name = "Sheet";
            [Tooltip("相对控制器的高度")]
            public float height = 2f;
            [Tooltip("水平尺寸（米）")]
            public Vector2 size = new Vector2(80f, 80f);
            [Range(0.05f, 2f)] public float density = 0.45f;
            [Range(0.2f, 3f)] public float layerSpeed = 1f;
            public float tiling = 0.04f;
            [Range(0.2f, 2f)] public float heightFade = 1f;
        }

        [Header("Prefab / Material")]
        [SerializeField] private Material sharedMaterial;
        [SerializeField] private Texture2D noiseA;
        [SerializeField] private Texture2D noiseB;

        [Header("Sheets (Near / Mid / Far height bands)")]
        [SerializeField] private SheetSettings[] sheets =
        {
            new SheetSettings { name = "AirNear", height = 1.2f, density = 0.42f, layerSpeed = 1.2f, tiling = 0.05f, heightFade = 1.1f, size = new Vector2(60f, 60f) },
            new SheetSettings { name = "AirMid", height = 3.5f, density = 0.28f, layerSpeed = 0.9f, tiling = 0.035f, heightFade = 0.9f, size = new Vector2(70f, 70f) },
            new SheetSettings { name = "AirFar", height = 7.0f, density = 0.18f, layerSpeed = 0.6f, tiling = 0.025f, heightFade = 0.7f, size = new Vector2(80f, 80f) },
        };

        [Header("Global Look")]
        [Tooltip("整体沙雾浓度倍率。0 = 几乎看不见，1 = 默认，>1 更浓")]
        [SerializeField, Range(0f, 2f)] private float globalDensity = 1f;
        [SerializeField] private Color colorNear = new Color(0.82f, 0.68f, 0.42f, 1f);
        [SerializeField] private Color colorFar = new Color(0.62f, 0.48f, 0.30f, 1f);
        [SerializeField] private float speedScale = 0.55f;
        [SerializeField] private float softDepthFade = 1.8f;

        [Header("Follow")]
        [SerializeField] private bool followTarget = true;
        [SerializeField] private Transform followTransform;
        [SerializeField] private Vector3 followOffset = Vector3.zero;
        [SerializeField, Tooltip("Only move when camera drifts farther than this (reduces hitch)")]
        private float followThreshold = 2f;

        Vector3 _followAnchor;
        bool _hasFollowAnchor;

        readonly System.Collections.Generic.List<Renderer> _renderers = new();
        readonly System.Collections.Generic.List<Material> _runtimeMats = new();

        public Material SharedMaterial => sharedMaterial;

        /// <summary>整体沙雾浓度倍率（0~2）。</summary>
        public float GlobalDensity
        {
            get => globalDensity;
            set
            {
                globalDensity = Mathf.Clamp(value, 0f, 2f);
                ApplyAll();
            }
        }

        public int SheetCount => sheets != null ? sheets.Length : 0;

        public float SpeedScale
        {
            get => speedScale;
            set
            {
                speedScale = Mathf.Max(0f, value);
                ApplyAll();
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
                Debug.LogWarning("[AirborneSandController] Multiple instances; last one wins.", this);
            Instance = this;
            EnsureSheets();
            ApplyAll();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            for (int i = 0; i < _runtimeMats.Count; i++)
            {
                if (_runtimeMats[i] == null)
                    continue;
                if (Application.isPlaying)
                    Destroy(_runtimeMats[i]);
                else
                    DestroyImmediate(_runtimeMats[i]);
            }
            _runtimeMats.Clear();
        }

        private void OnEnable() => ApplyAll();

        private void LateUpdate()
        {
            if (!followTarget)
                return;

            Transform t = followTransform != null
                ? followTransform
                : (Camera.main != null ? Camera.main.transform : null);

            if (t == null)
                return;

            Vector3 desired = t.position + followOffset;
            desired.y = transform.position.y;

            if (!_hasFollowAnchor)
            {
                _followAnchor = desired;
                _hasFollowAnchor = true;
                transform.position = desired;
                return;
            }

            // Snap only when far — avoid rewriting transform every frame (causes hitch / judder)
            Vector2 delta = new Vector2(desired.x - transform.position.x, desired.z - transform.position.z);
            if (delta.sqrMagnitude >= followThreshold * followThreshold)
                transform.position = desired;
        }

        private void OnValidate()
        {
            if (!isActiveAndEnabled)
                return;
            ApplyAll();
        }

        [ContextMenu("Rebuild Sheets")]
        public void RebuildSheets()
        {
            ClearChildren();
            _renderers.Clear();
            ClearRuntimeMats();
            EnsureSheets();
            ApplyAll();
        }

        public void ApplyAll()
        {
            if (sharedMaterial == null)
                return;

            EnsureSheets();

            for (int i = 0; i < _renderers.Count && i < sheets.Length; i++)
            {
                var r = _renderers[i];
                var s = sheets[i];
                if (r == null || s == null)
                    continue;

                r.transform.localPosition = new Vector3(0f, s.height, 0f);
                r.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                r.transform.localScale = new Vector3(s.size.x, s.size.y, 1f);

                Material mat = r.sharedMaterial;
                if (mat == null || mat.shader == null || mat.shader.name != "Sand/AirborneSand")
                {
                    mat = new Material(sharedMaterial)
                    {
                        name = sharedMaterial.name + "_" + s.name
                    };
                    _runtimeMats.Add(mat);
                    r.sharedMaterial = mat;
                }

                WriteSheet(mat, s);
            }
        }

        public void SetDensityMultiplier(float multiplier)
        {
            GlobalDensity = multiplier;
        }

        public float GetLayerDensity(int index)
        {
            if (!IsValidSheetIndex(index))
                return 0f;
            return sheets[index].density;
        }

        public float GetLayerSpeed(int index)
        {
            if (!IsValidSheetIndex(index))
                return 0f;
            return sheets[index].layerSpeed;
        }

        public void SetLayerDensity(int index, float density)
        {
            if (!IsValidSheetIndex(index))
                return;
            sheets[index].density = Mathf.Clamp(density, 0f, 2f);
            ApplyAll();
        }

        public void SetLayerSpeed(int index, float speed)
        {
            if (!IsValidSheetIndex(index))
                return;
            sheets[index].layerSpeed = Mathf.Max(0f, speed);
            ApplyAll();
        }

        public void SetLayer(int index, float density, float speed)
        {
            if (!IsValidSheetIndex(index))
                return;
            sheets[index].density = Mathf.Clamp(density, 0f, 2f);
            sheets[index].layerSpeed = Mathf.Max(0f, speed);
            ApplyAll();
        }

        /// <summary>一次设置近/中/远三层密度。长度不足时忽略缺项。</summary>
        public void SetAllLayerDensities(float near, float mid, float far)
        {
            if (SheetCount > 0) sheets[0].density = Mathf.Clamp(near, 0f, 2f);
            if (SheetCount > 1) sheets[1].density = Mathf.Clamp(mid, 0f, 2f);
            if (SheetCount > 2) sheets[2].density = Mathf.Clamp(far, 0f, 2f);
            ApplyAll();
        }

        /// <summary>一次设置近/中/远三层速度。</summary>
        public void SetAllLayerSpeeds(float near, float mid, float far)
        {
            if (SheetCount > 0) sheets[0].layerSpeed = Mathf.Max(0f, near);
            if (SheetCount > 1) sheets[1].layerSpeed = Mathf.Max(0f, mid);
            if (SheetCount > 2) sheets[2].layerSpeed = Mathf.Max(0f, far);
            ApplyAll();
        }

        bool IsValidSheetIndex(int index) => sheets != null && index >= 0 && index < sheets.Length;

        void WriteSheet(Material mat, SheetSettings s)
        {
            if (noiseA != null)
                mat.SetTexture(NoiseTexAId, noiseA);
            if (noiseB != null)
                mat.SetTexture(NoiseTexBId, noiseB);

            mat.SetFloat(SpeedScaleId, speedScale);
            mat.SetFloat(LayerSpeedId, s.layerSpeed);
            mat.SetFloat(DensityId, s.density * globalDensity);
            mat.SetFloat(TilingId, s.tiling);
            mat.SetFloat(SoftDepthFadeId, softDepthFade);
            mat.SetFloat(HeightFadeId, s.heightFade);
            mat.SetColor(ColorNearId, colorNear);
            mat.SetColor(ColorFarId, colorFar);
        }

        void EnsureSheets()
        {
            if (sheets == null || sheets.Length == 0)
                return;

            if (_renderers.Count == sheets.Length)
            {
                bool ok = true;
                for (int i = 0; i < _renderers.Count; i++)
                {
                    if (_renderers[i] == null)
                    {
                        ok = false;
                        break;
                    }
                }
                if (ok)
                    return;
            }

            _renderers.Clear();

            for (int i = 0; i < sheets.Length; i++)
            {
                var s = sheets[i];
                Transform child = transform.Find(s.name);
                GameObject go;
                if (child == null)
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    go.name = string.IsNullOrEmpty(s.name) ? $"AirSheet_{i}" : s.name;
                    go.transform.SetParent(transform, false);
                    var col = go.GetComponent<Collider>();
                    if (col != null)
                    {
                        if (Application.isPlaying)
                            Destroy(col);
                        else
                            DestroyImmediate(col);
                    }
                }
                else
                {
                    go = child.gameObject;
                }

                var renderer = go.GetComponent<MeshRenderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                _renderers.Add(renderer);
            }
        }

        void ClearChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var c = transform.GetChild(i).gameObject;
                if (Application.isPlaying)
                    Destroy(c);
                else
                    DestroyImmediate(c);
            }
        }

        void ClearRuntimeMats()
        {
            for (int i = 0; i < _runtimeMats.Count; i++)
            {
                if (_runtimeMats[i] == null)
                    continue;
                if (Application.isPlaying)
                    Destroy(_runtimeMats[i]);
                else
                    DestroyImmediate(_runtimeMats[i]);
            }
            _runtimeMats.Clear();
        }
    }
}
