using UnityEngine;

namespace SandSystem
{
    /// <summary>
    /// Directional Dissolve 控制器：与 WindManager 兼容，可动画化 Dissolve Strength。
    /// BlowAway = 沙子吹散物体；Cover = 沙子覆盖物体。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    public class DirectionalDissolveController : MonoBehaviour
    {
        static readonly int DissolveStrengthId = Shader.PropertyToID("_DissolveStrength");
        static readonly int EdgeWidthId = Shader.PropertyToID("_EdgeWidth");
        static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
        static readonly int EdgeIntensityId = Shader.PropertyToID("_EdgeIntensity");
        static readonly int NoiseTilingId = Shader.PropertyToID("_NoiseTiling");
        static readonly int NoiseStrengthId = Shader.PropertyToID("_NoiseStrength");
        static readonly int DissolveScaleId = Shader.PropertyToID("_DissolveScale");
        static readonly int DissolveOriginId = Shader.PropertyToID("_DissolveOrigin");
        static readonly int WindDirectionId = Shader.PropertyToID("_WindDirection");
        static readonly int WindSpeedId = Shader.PropertyToID("_WindSpeed");
        static readonly int UseLocalWindId = Shader.PropertyToID("_UseLocalWind");
        static readonly int InvertDirectionId = Shader.PropertyToID("_InvertDirection");

        public enum DissolveMode
        {
            BlowAway = 0,
            Cover = 1
        }

        [Header("Target")]
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private bool usePropertyBlock = true;

        [Header("Mode")]
        [SerializeField] private DissolveMode mode = DissolveMode.BlowAway;

        [Header("Dissolve")]
        [SerializeField, Range(0f, 1f)] private float dissolveStrength = 0.35f;
        [SerializeField] private float dissolveScale = 0.08f;
        [SerializeField] private float noiseTiling = 0.15f;
        [SerializeField, Range(0f, 2f)] private float noiseStrength = 0.55f;
        [SerializeField] private Vector3 dissolveOrigin;
        [SerializeField] private bool useRendererCenterAsOrigin = true;
        [SerializeField] private bool invertWindAxis;

        [Header("Edge")]
        [SerializeField] private Color edgeColor = new Color(1f, 0.72f, 0.35f, 1f);
        [SerializeField, Range(0.001f, 0.25f)] private float edgeWidth = 0.04f;
        [SerializeField, Range(0f, 8f)] private float edgeIntensity = 2.5f;

        [Header("Wind")]
        [SerializeField] private bool useLocalWind;
        [SerializeField] private Vector2 localWindDirection = new Vector2(1f, 0f);
        [SerializeField, Range(0f, 5f)] private float localWindSpeed = 1f;

        [Header("Animation (optional)")]
        [SerializeField] private bool animate;
        [SerializeField] private float animateFrom = 0f;
        [SerializeField] private float animateTo = 1f;
        [SerializeField] private float animateDuration = 4f;
        [SerializeField] private bool animateLoop;

        MaterialPropertyBlock _block;
        Material _runtimeMaterial;
        float _animateTime;

        public float DissolveStrength
        {
            get => dissolveStrength;
            set
            {
                dissolveStrength = Mathf.Clamp01(value);
                Apply();
            }
        }

        public DissolveMode Mode
        {
            get => mode;
            set
            {
                mode = value;
                ApplyModeKeyword();
                Apply();
            }
        }

        private void Awake()
        {
            if (targetRenderer == null)
                targetRenderer = GetComponent<Renderer>();
            if (usePropertyBlock)
                _block = new MaterialPropertyBlock();
            if (useRendererCenterAsOrigin && targetRenderer != null)
                dissolveOrigin = targetRenderer.bounds.center;
        }

        private void OnEnable()
        {
            _animateTime = 0f;
            ApplyModeKeyword();
            Apply();
        }

        private void Update()
        {
            if (!animate || animateDuration <= 0.01f)
                return;

            _animateTime += Time.deltaTime;
            float t = Mathf.Clamp01(_animateTime / animateDuration);
            dissolveStrength = Mathf.Lerp(animateFrom, animateTo, t);
            Apply();

            if (t >= 1f)
            {
                if (animateLoop)
                    _animateTime = 0f;
                else
                    animate = false;
            }
        }

        private void OnValidate()
        {
            if (targetRenderer == null)
                targetRenderer = GetComponent<Renderer>();
            ApplyModeKeyword();
            if (isActiveAndEnabled)
                Apply();
        }

        private void OnDestroy()
        {
            if (_runtimeMaterial == null)
                return;
            if (Application.isPlaying)
                Destroy(_runtimeMaterial);
            else
                DestroyImmediate(_runtimeMaterial);
        }

        [ContextMenu("Play Dissolve Animation")]
        public void PlayAnimation()
        {
            _animateTime = 0f;
            animate = true;
            dissolveStrength = animateFrom;
            Apply();
        }

        public void SetDissolveStrength(float value) => DissolveStrength = value;

        public void Apply()
        {
            if (targetRenderer == null)
                return;

            ResolveWind(out Vector2 dir, out float speed);
            Vector3 origin = useRendererCenterAsOrigin && targetRenderer != null
                ? targetRenderer.bounds.center
                : dissolveOrigin;

            if (usePropertyBlock)
            {
                if (_block == null)
                    _block = new MaterialPropertyBlock();
                targetRenderer.GetPropertyBlock(_block);
                WriteBlock(_block, dir, speed, origin);
                targetRenderer.SetPropertyBlock(_block);
            }
            else
            {
                EnsureRuntimeMaterial();
                WriteMaterial(_runtimeMaterial, dir, speed, origin);
            }
        }

        void ApplyModeKeyword()
        {
            if (targetRenderer == null)
                return;

            Material mat = Application.isPlaying
                ? (usePropertyBlock ? targetRenderer.sharedMaterial : targetRenderer.material)
                : targetRenderer.sharedMaterial;

            if (mat == null)
                return;

            // KeywordEnum: _DISSOLVEMODE_BLOWAWAY / _DISSOLVEMODE_COVER
            if (mode == DissolveMode.Cover)
            {
                mat.DisableKeyword("_DISSOLVEMODE_BLOWAWAY");
                mat.EnableKeyword("_DISSOLVEMODE_COVER");
            }
            else
            {
                mat.DisableKeyword("_DISSOLVEMODE_COVER");
                mat.EnableKeyword("_DISSOLVEMODE_BLOWAWAY");
            }
        }

        void ResolveWind(out Vector2 dir, out float speed)
        {
            if (useLocalWind)
            {
                dir = localWindDirection.sqrMagnitude > 1e-6f
                    ? localWindDirection.normalized
                    : Vector2.right;
                speed = localWindSpeed;
                return;
            }

            if (WindManager.Instance != null)
            {
                dir = WindManager.Instance.WindDirection;
                speed = WindManager.Instance.WindSpeed;
                return;
            }

            dir = Vector2.right;
            speed = 1f;
        }

        void EnsureRuntimeMaterial()
        {
            if (_runtimeMaterial != null)
                return;
            _runtimeMaterial = new Material(targetRenderer.sharedMaterial);
            targetRenderer.material = _runtimeMaterial;
        }

        void WriteBlock(MaterialPropertyBlock block, Vector2 dir, float speed, Vector3 origin)
        {
            block.SetFloat(DissolveStrengthId, dissolveStrength);
            block.SetFloat(EdgeWidthId, edgeWidth);
            block.SetColor(EdgeColorId, edgeColor);
            block.SetFloat(EdgeIntensityId, edgeIntensity);
            block.SetFloat(NoiseTilingId, noiseTiling);
            block.SetFloat(NoiseStrengthId, noiseStrength);
            block.SetFloat(DissolveScaleId, dissolveScale);
            block.SetVector(DissolveOriginId, origin);
            block.SetVector(WindDirectionId, new Vector4(dir.x, dir.y, 0f, 0f));
            block.SetFloat(WindSpeedId, speed);
            block.SetFloat(UseLocalWindId, useLocalWind ? 1f : 0f);
            block.SetFloat(InvertDirectionId, invertWindAxis ? 1f : 0f);
        }

        void WriteMaterial(Material mat, Vector2 dir, float speed, Vector3 origin)
        {
            mat.SetFloat(DissolveStrengthId, dissolveStrength);
            mat.SetFloat(EdgeWidthId, edgeWidth);
            mat.SetColor(EdgeColorId, edgeColor);
            mat.SetFloat(EdgeIntensityId, edgeIntensity);
            mat.SetFloat(NoiseTilingId, noiseTiling);
            mat.SetFloat(NoiseStrengthId, noiseStrength);
            mat.SetFloat(DissolveScaleId, dissolveScale);
            mat.SetVector(DissolveOriginId, origin);
            mat.SetVector(WindDirectionId, new Vector4(dir.x, dir.y, 0f, 0f));
            mat.SetFloat(WindSpeedId, speed);
            mat.SetFloat(UseLocalWindId, useLocalWind ? 1f : 0f);
            mat.SetFloat(InvertDirectionId, invertWindAxis ? 1f : 0f);
        }
    }
}
