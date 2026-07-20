using UnityEngine;

namespace SandSystem
{
    /// <summary>
    /// 沙漠天空 → 蓝天白云。由环境系统写入 Clearness / CloudCoverage。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sand/Skybox Controller")]
    public class SkyboxController : MonoBehaviour
    {
        public static SkyboxController Instance { get; private set; }

        static readonly int ClearnessId = Shader.PropertyToID("_Clearness");
        static readonly int CloudCoverageId = Shader.PropertyToID("_CloudCoverage");
        static readonly int ExposureId = Shader.PropertyToID("_Exposure");

        [SerializeField] private Material skyboxMaterial;
        [SerializeField, Range(0f, 1f)] private float clearness;
        [SerializeField, Range(0f, 1f)] private float cloudCoverage;
        [SerializeField] private float exposure = 1.05f;
        [SerializeField] private bool applyAmbientFromSky = true;

        private float _lastGiClearness = -1f;

        public float Clearness => clearness;
        public float CloudCoverage => cloudCoverage;
        public Material SkyboxMaterial => skyboxMaterial;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            Apply(forceGi: true);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnValidate()
        {
            if (Application.isPlaying) Apply(forceGi: false);
        }

        public void SetMaterial(Material mat)
        {
            skyboxMaterial = mat;
            Apply(forceGi: true);
        }

        public void SetClearness(float value)
        {
            SetSky(value, cloudCoverage);
        }

        public void SetCloudCoverage(float value)
        {
            SetSky(clearness, value);
        }

        public void SetSky(float clearnessValue, float cloudCoverageValue)
        {
            clearness = Mathf.Clamp01(clearnessValue);
            cloudCoverage = Mathf.Clamp01(cloudCoverageValue);
            Apply(forceGi: false);
        }

        public void Apply() => Apply(forceGi: true);

        void Apply(bool forceGi)
        {
            if (skyboxMaterial == null) return;

            if (skyboxMaterial.HasProperty(ClearnessId))
                skyboxMaterial.SetFloat(ClearnessId, clearness);
            if (skyboxMaterial.HasProperty(CloudCoverageId))
                skyboxMaterial.SetFloat(CloudCoverageId, cloudCoverage);
            if (skyboxMaterial.HasProperty(ExposureId))
                skyboxMaterial.SetFloat(ExposureId, exposure);

            if (RenderSettings.skybox != skyboxMaterial)
                RenderSettings.skybox = skyboxMaterial;

            if (applyAmbientFromSky)
            {
                Color dustAmb = new Color(0.55f, 0.42f, 0.28f);
                Color clearAmb = new Color(0.42f, 0.55f, 0.72f);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = Color.Lerp(dustAmb, clearAmb, clearness);
            }

            // DynamicGI 每帧刷新会造成天空“闪一下/跳一下”，只在变化明显时更新
            if (forceGi || Mathf.Abs(clearness - _lastGiClearness) >= 0.05f)
            {
                DynamicGI.UpdateEnvironment();
                _lastGiClearness = clearness;
            }
        }
    }
}
