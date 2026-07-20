using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SandSystem.Editor
{
    public static class SkyboxSetup
    {
        const string RootFolder = "Assets/SandPreview";
        const string MaterialPath = RootFolder + "/M_DesertSkybox.mat";
        const string NoisePath = RootFolder + "/Tex_NoiseA.png";

        [MenuItem("Sand/Setup Desert Skybox", priority = 6)]
        public static void Setup()
        {
            SetupQuiet();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            var panel = Object.FindFirstObjectByType<SkyboxController>();
            if (panel != null)
                Selection.activeGameObject = panel.gameObject;

            EditorUtility.DisplayDialog(
                "Desert Skybox",
                "已创建 SkySystem + M_DesertSkybox。\n\n" +
                "绿植达到阈值（默认 4000）后：\n" +
                "• 天空过渡到蓝天白云\n" +
                "• 沙尘密度 → 0\n" +
                "• 热浪减弱\n\n" +
                "阈值在 EnvironmentSystem → Clear Sky Tree Threshold 调整。",
                "OK");
        }

        /// <summary>无弹窗创建，供其他 Setup 调用。</summary>
        public static void SetupQuiet()
        {
            if (!AssetDatabase.IsValidFolder(RootFolder))
                AssetDatabase.CreateFolder("Assets", "SandPreview");

            Material mat = EnsureSkyboxMaterial();
            EnsureSkySystem(mat);
            RenderSettings.skybox = mat;
            DynamicGI.UpdateEnvironment();
        }

        [MenuItem("Sand/Select Sky System", priority = 23)]
        public static void SelectSky()
        {
            var sky = Object.FindFirstObjectByType<SkyboxController>();
            if (sky == null)
            {
                EditorUtility.DisplayDialog("Sky System", "未找到。请先运行 Sand / Setup Desert Skybox。", "OK");
                return;
            }
            Selection.activeGameObject = sky.gameObject;
        }

        public static Material EnsureSkyboxMaterial()
        {
            Shader shader = Shader.Find("Sand/DesertSkybox");
            if (shader == null)
                throw new System.Exception("找不到 Shader：Sand/DesertSkybox");

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }
            else
            {
                mat.shader = shader;
            }

            Texture2D noise = AssetDatabase.LoadAssetAtPath<Texture2D>(NoisePath);
            if (noise != null)
                mat.SetTexture("_NoiseTex", noise);

            mat.SetColor("_DustSkyColor", new Color(0.72f, 0.55f, 0.32f, 1f));
            mat.SetColor("_DustHorizonColor", new Color(0.85f, 0.68f, 0.42f, 1f));
            mat.SetColor("_ClearSkyColor", new Color(0.28f, 0.55f, 0.92f, 1f));
            mat.SetColor("_ClearHorizonColor", new Color(0.72f, 0.86f, 0.98f, 1f));
            mat.SetColor("_CloudColor", Color.white);
            mat.SetFloat("_CloudCoverage", 0.48f);
            mat.SetFloat("_CloudScale", 2.2f);
            mat.SetFloat("_CloudSpeed", 0.012f);
            mat.SetFloat("_Clearness", 0f);
            mat.SetFloat("_Exposure", 1.05f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static GameObject EnsureSkySystem(Material mat)
        {
            var existing = Object.FindFirstObjectByType<SkyboxController>();
            GameObject go;
            SkyboxController controller;
            if (existing != null)
            {
                go = existing.gameObject;
                controller = existing;
            }
            else
            {
                go = new GameObject("SkySystem");
                controller = go.AddComponent<SkyboxController>();
            }

            var so = new SerializedObject(controller);
            so.FindProperty("skyboxMaterial").objectReferenceValue = mat;
            so.FindProperty("clearness").floatValue = 0f;
            so.FindProperty("cloudCoverage").floatValue = 0.48f;
            so.ApplyModifiedPropertiesWithoutUndo();
            controller.Apply();
            return go;
        }
    }
}
