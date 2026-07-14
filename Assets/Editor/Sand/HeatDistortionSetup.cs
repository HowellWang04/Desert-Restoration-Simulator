using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace SandSystem.Editor
{
    public static class HeatDistortionSetup
    {
        const string RootFolder = "Assets/SandPreview";
        const string NoisePath = RootFolder + "/Tex_NoiseA.png";
        const string MaterialPath = RootFolder + "/M_HeatDistortion.mat";

        [MenuItem("Sand/Setup Heat Distortion (URP)", priority = 3)]
        public static void Setup()
        {
            if (!AssetDatabase.IsValidFolder(RootFolder))
                AssetDatabase.CreateFolder("Assets", "SandPreview");

            Texture2D noise = AssetDatabase.LoadAssetAtPath<Texture2D>(NoisePath);
            Material mat = CreateMaterial(noise);
            bool added = TryAddOrUpdateFeatureOnRenderer(mat, noise);
            var panel = EnsureScenePanel(mat, noise);

            Selection.activeGameObject = panel;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog(
                "Heat Distortion",
                "Heat panel created in scene: HeatSystem\n\n" +
                "Select HeatSystem in Hierarchy to tune:\n" +
                "• Enable Heat\n" +
                "• Distortion Strength / Noise Scale / Scroll Speed\n" +
                "• Depth / Height Mask\n\n" +
                (added
                    ? "Renderer Feature is on PC_Renderer.\n"
                    : "If effect missing: PC_Renderer → Add Renderer Feature → Heat Distortion\n") +
                "\nSame workflow as WindSystem / AirborneSand.",
                "OK");
        }

        [MenuItem("Sand/Select Heat System", priority = 21)]
        public static void SelectHeatSystem()
        {
            var heat = Object.FindFirstObjectByType<HeatDistortionController>();
            if (heat == null)
            {
                EditorUtility.DisplayDialog("Heat System", "No HeatSystem in scene. Run Sand / Setup Heat Distortion (URP).", "OK");
                return;
            }

            Selection.activeGameObject = heat.gameObject;
        }

        static GameObject EnsureScenePanel(Material mat, Texture2D noise)
        {
            var existing = Object.FindFirstObjectByType<HeatDistortionController>();
            GameObject go;
            HeatDistortionController controller;
            if (existing != null)
            {
                go = existing.gameObject;
                controller = existing;
            }
            else
            {
                go = new GameObject("HeatSystem");
                controller = go.AddComponent<HeatDistortionController>();
            }

            var so = new SerializedObject(controller);
            so.FindProperty("enableHeat").boolValue = true;
            so.FindProperty("distortionStrength").floatValue = 0.015f;
            so.FindProperty("noiseScale").floatValue = 8f;
            so.FindProperty("scrollSpeed").floatValue = 0.15f;
            so.FindProperty("heatMaterial").objectReferenceValue = mat;
            so.FindProperty("noiseTexture").objectReferenceValue = noise;
            so.ApplyModifiedPropertiesWithoutUndo();
            controller.Apply();
            return go;
        }

        static Material CreateMaterial(Texture2D noise)
        {
            Shader shader = Shader.Find("Hidden/Sand/HeatDistortion");
            if (shader == null)
                throw new System.Exception("Cannot find Shader: Hidden/Sand/HeatDistortion");

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

            if (noise != null)
                mat.SetTexture("_NoiseTex", noise);

            mat.SetFloat("_DistortionStrength", 0.015f);
            mat.SetFloat("_NoiseScale", 8f);
            mat.SetFloat("_ScrollSpeed", 0.15f);
            mat.EnableKeyword("_SOURCEMODE_BLITCOLOR");
            mat.DisableKeyword("_SOURCEMODE_OPAQUETEXTURE");
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        static bool TryAddOrUpdateFeatureOnRenderer(Material mat, Texture2D noise)
        {
            UniversalRendererData rendererData = FindPcRenderer();
            if (rendererData == null)
                return false;

            HeatDistortionRendererFeature existing = null;
            foreach (var f in rendererData.rendererFeatures)
            {
                if (f is HeatDistortionRendererFeature heat)
                {
                    existing = heat;
                    break;
                }
            }

            if (existing != null)
            {
                ApplyFeatureDefaults(existing, mat, noise);
                EditorUtility.SetDirty(existing);
                EditorUtility.SetDirty(rendererData);
                AssetDatabase.SaveAssets();
                return true;
            }

            var feature = ScriptableObject.CreateInstance<HeatDistortionRendererFeature>();
            feature.name = "HeatDistortion";
            ApplyFeatureDefaults(feature, mat, noise);
            AssetDatabase.AddObjectToAsset(feature, rendererData);

            var so = new SerializedObject(rendererData);
            var featuresProp = so.FindProperty("m_RendererFeatures");
            featuresProp.arraySize++;
            featuresProp.GetArrayElementAtIndex(featuresProp.arraySize - 1).objectReferenceValue = feature;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(rendererData));
            return true;
        }

        static void ApplyFeatureDefaults(HeatDistortionRendererFeature feature, Material mat, Texture2D noise)
        {
            feature.settings.isEnabled = true;
            feature.settings.material = mat;
            feature.settings.noiseTexture = noise;
            feature.settings.distortionStrength = 0.015f;
            feature.settings.noiseScale = 8f;
            feature.settings.scrollSpeed = 0.15f;
            feature.settings.sourceMode = HeatDistortionRendererFeature.SourceMode.BlitColor;
            feature.settings.injectionPoint = HeatDistortionRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
        }

        static UniversalRendererData FindPcRenderer()
        {
            string[] guids = AssetDatabase.FindAssets("t:UniversalRendererData");
            UniversalRendererData fallback = null;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (data == null)
                    continue;
                if (path.Contains("PC_Renderer"))
                    return data;
                fallback ??= data;
            }
            return fallback;
        }
    }
}
