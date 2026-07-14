using UnityEngine;

namespace SandSystem
{
    /// <summary>
    /// 写入 GroundSand 外观参数。默认不每帧改材质（滚动由 GPU 时间完成）。
    /// </summary>
    [DisallowMultipleComponent]
    public class GroundSandController : MonoBehaviour
    {
        static readonly int WindDirectionId = Shader.PropertyToID("_WindDirection");
        static readonly int WindSpeedId = Shader.PropertyToID("_WindSpeed");
        static readonly int UseLocalWindId = Shader.PropertyToID("_UseLocalWind");
        static readonly int SpeedScaleId = Shader.PropertyToID("_SpeedScale");
        static readonly int WorldUVScaleId = Shader.PropertyToID("_WorldUVScale");
        static readonly int NoiseBlendId = Shader.PropertyToID("_NoiseBlend");
        static readonly int ContrastId = Shader.PropertyToID("_Contrast");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int AlphaPowerId = Shader.PropertyToID("_AlphaPower");
        static readonly int ParallaxAmplitudeId = Shader.PropertyToID("_ParallaxAmplitude");
        static readonly int ParallaxHeightScaleId = Shader.PropertyToID("_ParallaxHeightScale");

        static readonly int FarTilingId = Shader.PropertyToID("_FarTiling");
        static readonly int FarSpeedId = Shader.PropertyToID("_FarSpeed");
        static readonly int FarParallaxId = Shader.PropertyToID("_FarParallax");
        static readonly int FarAlphaId = Shader.PropertyToID("_FarAlpha");
        static readonly int FarTintId = Shader.PropertyToID("_FarTint");

        static readonly int MidTilingId = Shader.PropertyToID("_MidTiling");
        static readonly int MidSpeedId = Shader.PropertyToID("_MidSpeed");
        static readonly int MidParallaxId = Shader.PropertyToID("_MidParallax");
        static readonly int MidAlphaId = Shader.PropertyToID("_MidAlpha");
        static readonly int MidTintId = Shader.PropertyToID("_MidTint");

        static readonly int NearTilingId = Shader.PropertyToID("_NearTiling");
        static readonly int NearSpeedId = Shader.PropertyToID("_NearSpeed");
        static readonly int NearParallaxId = Shader.PropertyToID("_NearParallax");
        static readonly int NearAlphaId = Shader.PropertyToID("_NearAlpha");
        static readonly int NearTintId = Shader.PropertyToID("_NearTint");

        static readonly int ColorDeepId = Shader.PropertyToID("_ColorDeep");
        static readonly int ColorMidId = Shader.PropertyToID("_ColorMid");
        static readonly int ColorBrightId = Shader.PropertyToID("_ColorBright");

        [Header("Target")]
        [SerializeField] private Terrain targetTerrain;
        [SerializeField] private Renderer targetRenderer;

        [Header("Override Wind (optional)")]
        [Tooltip("开启后使用下方本地风，并写入材质（全局 WindManager 仍会被 Shader 优先，除非关掉 WindManager）")]
        [SerializeField] private bool overrideWind;
        [SerializeField] private Vector2 localWindDirection = new Vector2(1f, 0f);
        [SerializeField, Range(0f, 5f)] private float localWindSpeed = 1f;

        [Header("Global")]
        [SerializeField] private float speedScale = 0.45f;
        [SerializeField] private float worldUVScale = 0.12f;
        [SerializeField, Range(0f, 1f)] private float noiseBlend = 0.5f;
        [SerializeField, Range(0.1f, 4f)] private float contrast = 1.35f;
        [SerializeField, Range(0f, 1f)] private float alpha = 1f;
        [SerializeField, Range(0.1f, 4f)] private float alphaPower = 1f;
        [SerializeField, Range(0f, 0.08f)] private float parallaxAmplitude = 0f;
        [SerializeField, Range(0f, 2f)] private float parallaxHeightScale = 0f;

        [Header("Layer Far")]
        [SerializeField] private float farTiling = 2.5f;
        [SerializeField] private float farSpeed = 0.45f;
        [SerializeField, Range(0f, 2f)] private float farParallax = 0.25f;
        [SerializeField, Range(0f, 1f)] private float farAlpha = 0.6f;
        [SerializeField] private Color farTint = Color.white;

        [Header("Layer Mid")]
        [SerializeField] private float midTiling = 4.5f;
        [SerializeField] private float midSpeed = 1f;
        [SerializeField, Range(0f, 2f)] private float midParallax = 0.55f;
        [SerializeField, Range(0f, 1f)] private float midAlpha = 0.75f;
        [SerializeField] private Color midTint = new Color(1f, 0.98f, 0.94f, 1f);

        [Header("Layer Near")]
        [SerializeField] private float nearTiling = 7.5f;
        [SerializeField] private float nearSpeed = 1.8f;
        [SerializeField, Range(0f, 2f)] private float nearParallax = 0.9f;
        [SerializeField, Range(0f, 1f)] private float nearAlpha = 0.9f;
        [SerializeField] private Color nearTint = new Color(1f, 0.95f, 0.88f, 1f);

        [Header("Colors")]
        [SerializeField] private Color colorDeep = new Color(0.55f, 0.38f, 0.18f, 1f);
        [SerializeField] private Color colorMid = new Color(0.78f, 0.60f, 0.32f, 1f);
        [SerializeField] private Color colorBright = new Color(0.95f, 0.82f, 0.55f, 1f);

        public float Alpha
        {
            get => alpha;
            set { alpha = Mathf.Clamp01(value); Apply(); }
        }

        private void Awake() => CacheTargets();

        private void OnEnable() => Apply();

        private void OnValidate()
        {
            CacheTargets();
            Apply();
        }

        void CacheTargets()
        {
            if (targetTerrain == null)
                targetTerrain = GetComponent<Terrain>();
            if (targetRenderer == null)
                targetRenderer = GetComponent<Renderer>();
        }

        /// <summary>把外观参数写入材质（改 Inspector 时调用即可，无需每帧）。</summary>
        public void Apply()
        {
            CacheTargets();
            ResolveWind(out Vector2 dir, out float speed);

            Material mat = null;
            if (targetTerrain != null)
                mat = targetTerrain.materialTemplate;
            else if (targetRenderer != null)
                mat = Application.isPlaying ? targetRenderer.material : targetRenderer.sharedMaterial;

            if (mat == null)
                return;

            WriteProperties(mat, dir, speed);
        }

        public void SetLocalWind(Vector2 direction, float speed)
        {
            overrideWind = true;
            localWindDirection = direction;
            localWindSpeed = Mathf.Max(0f, speed);
            Apply();
        }

        public void ClearLocalWindOverride()
        {
            overrideWind = false;
            Apply();
        }

        void ResolveWind(out Vector2 dir, out float speed)
        {
            if (overrideWind)
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

        void WriteProperties(Material mat, Vector2 dir, float speed)
        {
            mat.SetVector(WindDirectionId, new Vector4(dir.x, dir.y, 0f, 0f));
            mat.SetFloat(WindSpeedId, speed);
            mat.SetFloat(UseLocalWindId, overrideWind ? 1f : 0f);
            mat.SetFloat(SpeedScaleId, speedScale);
            mat.SetFloat(WorldUVScaleId, worldUVScale);
            mat.SetFloat(NoiseBlendId, noiseBlend);
            mat.SetFloat(ContrastId, contrast);
            mat.SetFloat(AlphaId, alpha);
            mat.SetFloat(AlphaPowerId, alphaPower);
            mat.SetFloat(ParallaxAmplitudeId, parallaxAmplitude);
            mat.SetFloat(ParallaxHeightScaleId, parallaxHeightScale);

            mat.SetFloat(FarTilingId, farTiling);
            mat.SetFloat(FarSpeedId, farSpeed);
            mat.SetFloat(FarParallaxId, farParallax);
            mat.SetFloat(FarAlphaId, farAlpha);
            mat.SetColor(FarTintId, farTint);

            mat.SetFloat(MidTilingId, midTiling);
            mat.SetFloat(MidSpeedId, midSpeed);
            mat.SetFloat(MidParallaxId, midParallax);
            mat.SetFloat(MidAlphaId, midAlpha);
            mat.SetColor(MidTintId, midTint);

            mat.SetFloat(NearTilingId, nearTiling);
            mat.SetFloat(NearSpeedId, nearSpeed);
            mat.SetFloat(NearParallaxId, nearParallax);
            mat.SetFloat(NearAlphaId, nearAlpha);
            mat.SetColor(NearTintId, nearTint);

            mat.SetColor(ColorDeepId, colorDeep);
            mat.SetColor(ColorMidId, colorMid);
            mat.SetColor(ColorBrightId, colorBright);
        }
    }
}
