using UnityEngine;

namespace SandSystem
{
    /// <summary>
    /// 风 / 沙 / 热浪 统一对外 API。
    /// 其他系统（决策、环境指数）只依赖此类，不要直接改各 Controller 内部字段。
    /// </summary>
    /// <example>
    /// DesertFxAPI.SetWindDirection(new Vector2(1, 0.3f));
    /// DesertFxAPI.SetWindStrength(1.5f);
    /// DesertFxAPI.SetSandGlobalDensity(0.6f);
    /// DesertFxAPI.SetSandLayer(0, density: 0.4f, speed: 1.2f);
    /// DesertFxAPI.SetHeatDistortionStrength(0.02f);
    /// </example>
    public static class DesertFxAPI
    {
        // -------- Wind --------

        /// <summary>风向（世界 XZ，会自动归一化）。</summary>
        public static Vector2 GetWindDirection()
        {
            return WindManager.Instance != null ? WindManager.Instance.WindDirection : Vector2.right;
        }

        /// <summary>配置风速（0~5 建议），不含阵风。</summary>
        public static float GetWindStrength()
        {
            return WindManager.Instance != null ? WindManager.Instance.BaseWindSpeed : 0f;
        }

        public static void SetWindDirection(Vector2 direction)
        {
            if (WindManager.Instance == null)
            {
                Debug.LogWarning("[DesertFxAPI] WindManager missing.");
                return;
            }
            WindManager.Instance.SetWindDirection(direction);
        }

        public static void SetWindStrength(float strength)
        {
            if (WindManager.Instance == null)
            {
                Debug.LogWarning("[DesertFxAPI] WindManager missing.");
                return;
            }
            WindManager.Instance.SetWindSpeed(strength);
        }

        public static void SetWind(Vector2 direction, float strength)
        {
            if (WindManager.Instance == null)
            {
                Debug.LogWarning("[DesertFxAPI] WindManager missing.");
                return;
            }
            WindManager.Instance.SetWind(direction, strength);
        }

        // -------- Sand (Airborne) --------

        public static float GetSandGlobalDensity()
        {
            return AirborneSandController.Instance != null
                ? AirborneSandController.Instance.GlobalDensity
                : 0f;
        }

        public static void SetSandGlobalDensity(float density)
        {
            if (AirborneSandController.Instance == null)
            {
                Debug.LogWarning("[DesertFxAPI] AirborneSandController missing.");
                return;
            }
            AirborneSandController.Instance.GlobalDensity = density;
        }

        public static int GetSandLayerCount()
        {
            return AirborneSandController.Instance != null
                ? AirborneSandController.Instance.SheetCount
                : 0;
        }

        /// <param name="layerIndex">0=Near, 1=Mid, 2=Far</param>
        public static float GetSandLayerDensity(int layerIndex)
        {
            return AirborneSandController.Instance != null
                ? AirborneSandController.Instance.GetLayerDensity(layerIndex)
                : 0f;
        }

        /// <param name="layerIndex">0=Near, 1=Mid, 2=Far</param>
        public static float GetSandLayerSpeed(int layerIndex)
        {
            return AirborneSandController.Instance != null
                ? AirborneSandController.Instance.GetLayerSpeed(layerIndex)
                : 0f;
        }

        public static void SetSandLayerDensity(int layerIndex, float density)
        {
            if (AirborneSandController.Instance == null)
            {
                Debug.LogWarning("[DesertFxAPI] AirborneSandController missing.");
                return;
            }
            AirborneSandController.Instance.SetLayerDensity(layerIndex, density);
        }

        public static void SetSandLayerSpeed(int layerIndex, float speed)
        {
            if (AirborneSandController.Instance == null)
            {
                Debug.LogWarning("[DesertFxAPI] AirborneSandController missing.");
                return;
            }
            AirborneSandController.Instance.SetLayerSpeed(layerIndex, speed);
        }

        public static void SetSandLayer(int layerIndex, float density, float speed)
        {
            if (AirborneSandController.Instance == null)
            {
                Debug.LogWarning("[DesertFxAPI] AirborneSandController missing.");
                return;
            }
            AirborneSandController.Instance.SetLayer(layerIndex, density, speed);
        }

        public static void SetSandAllLayerDensities(float near, float mid, float far)
        {
            if (AirborneSandController.Instance == null)
            {
                Debug.LogWarning("[DesertFxAPI] AirborneSandController missing.");
                return;
            }
            AirborneSandController.Instance.SetAllLayerDensities(near, mid, far);
        }

        public static void SetSandAllLayerSpeeds(float near, float mid, float far)
        {
            if (AirborneSandController.Instance == null)
            {
                Debug.LogWarning("[DesertFxAPI] AirborneSandController missing.");
                return;
            }
            AirborneSandController.Instance.SetAllLayerSpeeds(near, mid, far);
        }

        // -------- Heat --------

        public static float GetHeatDistortionStrength()
        {
            return HeatDistortionController.Instance != null
                ? HeatDistortionController.Instance.DistortionStrength
                : 0f;
        }

        public static void SetHeatDistortionStrength(float strength)
        {
            if (HeatDistortionController.Instance == null)
            {
                Debug.LogWarning("[DesertFxAPI] HeatDistortionController missing.");
                return;
            }
            HeatDistortionController.Instance.DistortionStrength = strength;
        }

        public static void SetHeatEnabled(bool enabled)
        {
            if (HeatDistortionController.Instance == null)
            {
                Debug.LogWarning("[DesertFxAPI] HeatDistortionController missing.");
                return;
            }
            HeatDistortionController.Instance.EnableHeat = enabled;
        }

        // -------- Snapshot (for environment system) --------

        [System.Serializable]
        public struct FxSnapshot
        {
            public Vector2 windDirection;
            public float windStrength;
            public float sandGlobalDensity;
            public float sandNearDensity;
            public float sandMidDensity;
            public float sandFarDensity;
            public float sandNearSpeed;
            public float sandMidSpeed;
            public float sandFarSpeed;
            public float heatDistortionStrength;
            public float skyClearness;
            public float skyCloudCoverage;
        }

        public static FxSnapshot CaptureSnapshot()
        {
            return new FxSnapshot
            {
                windDirection = GetWindDirection(),
                windStrength = GetWindStrength(),
                sandGlobalDensity = GetSandGlobalDensity(),
                sandNearDensity = GetSandLayerDensity(0),
                sandMidDensity = GetSandLayerDensity(1),
                sandFarDensity = GetSandLayerDensity(2),
                sandNearSpeed = GetSandLayerSpeed(0),
                sandMidSpeed = GetSandLayerSpeed(1),
                sandFarSpeed = GetSandLayerSpeed(2),
                heatDistortionStrength = GetHeatDistortionStrength(),
                skyClearness = GetSkyClearness(),
                skyCloudCoverage = GetSkyCloudCoverage()
            };
        }

        public static void ApplySnapshot(FxSnapshot s)
        {
            SetWind(s.windDirection, s.windStrength);
            SetSandGlobalDensity(s.sandGlobalDensity);
            SetSandAllLayerDensities(s.sandNearDensity, s.sandMidDensity, s.sandFarDensity);
            SetSandAllLayerSpeeds(s.sandNearSpeed, s.sandMidSpeed, s.sandFarSpeed);
            SetHeatDistortionStrength(s.heatDistortionStrength);
            SetSky(s.skyClearness, s.skyCloudCoverage);
        }

        // -------- Sky --------

        public static float GetSkyClearness()
        {
            return SkyboxController.Instance != null ? SkyboxController.Instance.Clearness : 0f;
        }

        public static float GetSkyCloudCoverage()
        {
            return SkyboxController.Instance != null ? SkyboxController.Instance.CloudCoverage : 0f;
        }

        public static void SetSkyClearness(float clearness)
        {
            if (SkyboxController.Instance == null)
            {
                Debug.LogWarning("[DesertFxAPI] SkyboxController missing.");
                return;
            }
            SkyboxController.Instance.SetClearness(clearness);
        }

        public static void SetSkyCloudCoverage(float coverage)
        {
            if (SkyboxController.Instance == null)
            {
                Debug.LogWarning("[DesertFxAPI] SkyboxController missing.");
                return;
            }
            SkyboxController.Instance.SetCloudCoverage(coverage);
        }

        public static void SetSky(float clearness, float cloudCoverage)
        {
            if (SkyboxController.Instance == null)
            {
                Debug.LogWarning("[DesertFxAPI] SkyboxController missing.");
                return;
            }
            SkyboxController.Instance.SetSky(clearness, cloudCoverage);
        }
    }
}
