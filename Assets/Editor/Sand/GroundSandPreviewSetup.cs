#if UNITY_EDITOR
using System.IO;
using SandSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SandSystem.Editor
{
    /// <summary>
    /// 一键生成可雕刻的 Terrain + GroundSandTerrain 流沙预览。
    /// 菜单：Sand / Setup Ground Sand Terrain Preview
    /// </summary>
    public static class GroundSandPreviewSetup
    {
        const string RootFolder = "Assets/SandPreview";
        const string NoiseAPath = RootFolder + "/Tex_NoiseA.png";
        const string NoiseBPath = RootFolder + "/Tex_NoiseB.png";
        const string MaterialPath = RootFolder + "/M_GroundSandTerrain.mat";
        const string TerrainDataPath = RootFolder + "/TD_SandPreview.asset";

        [MenuItem("Sand/Setup Ground Sand Terrain Preview", priority = 0)]
        public static void SetupTerrainPreview()
        {
            EnsureFolders();
            Texture2D noiseA = CreateOrLoadNoise(NoiseAPath, seed: 11, scale: 6f, octaves: 4);
            Texture2D noiseB = CreateOrLoadNoise(NoiseBPath, seed: 29, scale: 11f, octaves: 5);
            Material mat = CreateOrUpdateTerrainMaterial(noiseA, noiseB);
            BuildTerrainPreview(mat);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Ground Sand Terrain Preview",
                "Terrain 流沙预览已生成。\n\n" +
                "1. 按 Play 查看近/中/远三层 Parallax 流沙\n" +
                "2. 用 Terrain 工具 Raise/Lower 雕刻高度\n" +
                "3. 选中 WindSystem 调节风向风速\n" +
                "4. 选中 SandTerrain 上的 GroundSandController 调层参数\n\n" +
                "资源：Assets/SandPreview/",
                "OK");
        }

        [MenuItem("Sand/Setup Ground Sand Preview (Plane Legacy)", priority = 11)]
        public static void SetupPlaneLegacy()
        {
            EnsureFolders();
            Texture2D noiseA = CreateOrLoadNoise(NoiseAPath, seed: 11, scale: 6f, octaves: 4);
            Texture2D noiseB = CreateOrLoadNoise(NoiseBPath, seed: 29, scale: 11f, octaves: 5);

            Shader meshShader = Shader.Find("Sand/GroundSand");
            if (meshShader == null)
            {
                EditorUtility.DisplayDialog("Error", "找不到 Sand/GroundSand", "OK");
                return;
            }

            string meshMatPath = RootFolder + "/M_GroundSand.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(meshMatPath);
            if (mat == null)
            {
                mat = new Material(meshShader);
                AssetDatabase.CreateAsset(mat, meshMatPath);
            }
            else
            {
                mat.shader = meshShader;
            }

            ApplyCommonMaterialParams(mat, noiseA, noiseB);
            EditorUtility.SetDirty(mat);

            EnsureCameraAndLight();
            EnsureWind();

            var oldPlane = GameObject.Find("SandGround");
            if (oldPlane == null)
            {
                oldPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
                oldPlane.name = "SandGround";
            }

            oldPlane.transform.position = Vector3.zero;
            oldPlane.transform.localScale = new Vector3(2.5f, 1f, 2.5f);
            oldPlane.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (oldPlane.GetComponent<GroundSandController>() == null)
                oldPlane.AddComponent<GroundSandController>();

            Selection.activeGameObject = oldPlane;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Sand/Select Wind Manager", priority = 20)]
        public static void SelectWindManager()
        {
            var wind = Object.FindFirstObjectByType<WindManager>();
            if (wind == null)
            {
                EditorUtility.DisplayDialog("Wind Manager", "请先执行 Sand / Setup Ground Sand Terrain Preview。", "OK");
                return;
            }

            Selection.activeGameObject = wind.gameObject;
        }

        static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/SandPreview"))
                AssetDatabase.CreateFolder("Assets", "SandPreview");
        }

        static Texture2D CreateOrLoadNoise(string assetPath, int seed, float scale, int octaves)
        {
            return CreateOrLoadNoise(assetPath, seed, scale, octaves, forceReseamless: false);
        }

        static Texture2D CreateOrLoadNoise(string assetPath, int seed, float scale, int octaves, bool forceReseamless)
        {
            if (File.Exists(assetPath) && !forceReseamless)
                return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);

            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            var rng = new System.Random(seed);
            float ox = (float)rng.NextDouble() * 100f;
            float oy = (float)rng.NextDouble() * 100f;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float n = SeamlessFbm(x, y, size, scale, octaves, ox, oy);
                tex.SetPixel(x, y, new Color(n, n, n, 1f));
            }

            tex.Apply(false, false);
            File.WriteAllBytes(assetPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.sRGBTexture = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        // Toroidal mapping → seamless tile (avoids wrap seams that look like flow pauses)
        static float SeamlessFbm(int x, int y, int size, float scale, int octaves, float ox, float oy)
        {
            float u = x / (float)size;
            float v = y / (float)size;
            float s = u * Mathf.PI * 2f;
            float t = v * Mathf.PI * 2f;

            float value = 0f, amp = 0.5f, freq = 1f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                float nx = Mathf.Cos(s) * scale * freq + ox + i * 19.1f;
                float ny = Mathf.Sin(s) * scale * freq + oy + i * 7.3f;
                float nz = Mathf.Cos(t) * scale * freq + ox + 31.7f + i * 5.5f;
                float nw = Mathf.Sin(t) * scale * freq + oy + 13.9f + i * 11.2f;
                float n = (Mathf.PerlinNoise(nx, ny) + Mathf.PerlinNoise(nz, nw)) * 0.5f;
                value += n * amp;
                norm += amp;
                amp *= 0.5f;
                freq *= 2f;
            }

            return Mathf.Clamp01(value / norm);
        }

        static float Fbm(float x, float y, int octaves)
        {
            float value = 0f, amp = 0.5f, freq = 1f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                value += Mathf.PerlinNoise(x * freq, y * freq) * amp;
                norm += amp;
                amp *= 0.5f;
                freq *= 2f;
            }
            return Mathf.Clamp01(value / norm);
        }

        [MenuItem("Sand/Regenerate Seamless Noise Textures", priority = 30)]
        public static void RegenerateSeamlessNoise()
        {
            EnsureFolders();
            CreateOrLoadNoise(NoiseAPath, seed: 11, scale: 6f, octaves: 4, forceReseamless: true);
            CreateOrLoadNoise(NoiseBPath, seed: 29, scale: 11f, octaves: 5, forceReseamless: true);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog(
                "Seamless Noise",
                "已重新生成无缝噪声贴图。\n请重新 Play，流向接缝造成的停顿感应消失。",
                "OK");
        }

        static Material CreateOrUpdateTerrainMaterial(Texture2D noiseA, Texture2D noiseB)
        {
            Shader shader = Shader.Find("Sand/GroundSandTerrain");
            if (shader == null)
                throw new System.Exception("找不到 Shader：Sand/GroundSandTerrain");

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

            ApplyCommonMaterialParams(mat, noiseA, noiseB);
            mat.EnableKeyword("_TERRAIN_INSTANCED_PERPIXEL_NORMAL");
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void ApplyCommonMaterialParams(Material mat, Texture2D noiseA, Texture2D noiseB)
        {
            mat.SetTexture("_NoiseTexA", noiseA);
            mat.SetTexture("_NoiseTexB", noiseB);
            mat.SetFloat("_SpeedScale", 0.45f);
            mat.SetFloat("_WorldUVScale", 0.12f);
            mat.SetFloat("_WindSpeed", 1.25f);
            mat.SetFloat("_ParallaxAmplitude", 0f);
            mat.SetFloat("_ParallaxHeightScale", 0f);
            mat.SetFloat("_FarSpeed", 0.5f);
            mat.SetFloat("_MidSpeed", 1.0f);
            mat.SetFloat("_NearSpeed", 1.6f);
            mat.SetFloat("_FarAlpha", 0.6f);
            mat.SetFloat("_MidAlpha", 0.75f);
            mat.SetFloat("_NearAlpha", 0.9f);
            mat.SetColor("_ColorDeep", new Color(0.55f, 0.38f, 0.18f, 1f));
            mat.SetColor("_ColorMid", new Color(0.78f, 0.60f, 0.32f, 1f));
            mat.SetColor("_ColorBright", new Color(0.95f, 0.82f, 0.55f, 1f));
        }

        static void BuildTerrainPreview(Material mat)
        {
            EnsureCameraAndLight();
            EnsureWind();

            // 移除旧 Plane 预览，避免叠在一起
            var oldPlane = GameObject.Find("SandGround");
            if (oldPlane != null)
                Object.DestroyImmediate(oldPlane);

            Terrain existing = null;
            var terrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            foreach (var t in terrains)
            {
                if (t.name == "SandTerrain")
                {
                    existing = t;
                    break;
                }
            }

            TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
            if (data == null)
            {
                data = new TerrainData();
                data.heightmapResolution = 513;
                data.size = new Vector3(60f, 12f, 60f);
                StampPreviewDunes(data);
                AssetDatabase.CreateAsset(data, TerrainDataPath);
            }

            GameObject terrainGo;
            Terrain terrain;
            if (existing != null)
            {
                terrainGo = existing.gameObject;
                terrain = existing;
                terrain.terrainData = data;
            }
            else
            {
                terrainGo = Terrain.CreateTerrainGameObject(data);
                terrainGo.name = "SandTerrain";
                terrain = terrainGo.GetComponent<Terrain>();
            }

            terrainGo.transform.position = new Vector3(-30f, 0f, -30f);
            terrain.materialTemplate = mat;
            terrain.drawInstanced = true;
            terrain.allowAutoConnect = true;

            if (terrainGo.GetComponent<GroundSandController>() == null)
                terrainGo.AddComponent<GroundSandController>();

            Selection.activeGameObject = terrainGo;
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.LookAt(terrainGo.transform.position + new Vector3(30f, 0f, 30f), Quaternion.Euler(35f, 0f, 0f), 40f);
            }
        }

        static void StampPreviewDunes(TerrainData data)
        {
            int res = data.heightmapResolution;
            float[,] heights = new float[res, res];
            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float u = x / (float)(res - 1);
                float v = y / (float)(res - 1);
                float d1 = Mathf.PerlinNoise(u * 3.2f, v * 3.2f);
                float d2 = Mathf.PerlinNoise(u * 7.1f + 20f, v * 7.1f + 10f);
                heights[y, x] = Mathf.Clamp01(d1 * 0.45f + d2 * 0.2f + 0.08f);
            }
            data.SetHeights(0, 0, heights);
        }

        static void EnsureCameraAndLight()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                cam = camGo.AddComponent<Camera>();
                camGo.tag = "MainCamera";
                camGo.AddComponent<AudioListener>();
            }

            cam.transform.position = new Vector3(0f, 18f, -28f);
            cam.transform.rotation = Quaternion.Euler(28f, 0f, 0f);
            cam.clearFlags = CameraClearFlags.Skybox;

            if (Object.FindFirstObjectByType<Light>() == null)
            {
                var lightGo = new GameObject("Directional Light");
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.25f;
                lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
        }

        static void EnsureWind()
        {
            WindManager wind = Object.FindFirstObjectByType<WindManager>();
            if (wind == null)
            {
                var windGo = new GameObject("WindSystem");
                wind = windGo.AddComponent<WindManager>();
            }
            wind.SetWind(new Vector2(1f, 0.35f), 1.25f);
        }

        const string AirborneMatPath = RootFolder + "/M_AirborneSand.mat";

        [MenuItem("Sand/Setup Airborne Sand Preview", priority = 1)]
        public static void SetupAirbornePreview()
        {
            EnsureFolders();
            EnsureCameraAndLight();
            EnsureWind();

            Texture2D noiseA = CreateOrLoadNoise(NoiseAPath, seed: 11, scale: 6f, octaves: 4);
            Texture2D noiseB = CreateOrLoadNoise(NoiseBPath, seed: 29, scale: 11f, octaves: 5);
            Material mat = CreateOrUpdateAirborneMaterial(noiseA, noiseB);

            AirborneSandController existing = Object.FindFirstObjectByType<AirborneSandController>();
            GameObject root;
            AirborneSandController controller;
            if (existing != null)
            {
                root = existing.gameObject;
                controller = existing;
            }
            else
            {
                root = new GameObject("AirborneSand");
                controller = root.AddComponent<AirborneSandController>();
            }

            root.transform.position = new Vector3(0f, 0f, 0f);

            var so = new SerializedObject(controller);
            so.FindProperty("sharedMaterial").objectReferenceValue = mat;
            so.FindProperty("noiseA").objectReferenceValue = noiseA;
            so.FindProperty("noiseB").objectReferenceValue = noiseB;
            so.FindProperty("followTarget").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            controller.RebuildSheets();
            controller.ApplyAll();

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog(
                "Airborne Sand Preview",
                "Airborne sand sheets created.\n\n" +
                "1. WindSystem is required (auto-checked)\n" +
                "2. Play to see near/mid/far sheets flowing with wind\n" +
                "3. Select AirborneSand to tune Density / Soft Depth / heights\n" +
                "4. If flow looks stuttery: Sand / Regenerate Seamless Noise Textures\n" +
                "5. URP Depth Texture must be On (PC_RPAsset already On)\n\n" +
                "Material: Assets/SandPreview/M_AirborneSand.mat",
                "OK");
        }

        static Material CreateOrUpdateAirborneMaterial(Texture2D noiseA, Texture2D noiseB)
        {
            Shader shader = Shader.Find("Sand/AirborneSand");
            if (shader == null)
                throw new System.Exception("Cannot find Shader: Sand/AirborneSand");

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(AirborneMatPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, AirborneMatPath);
            }
            else
            {
                mat.shader = shader;
            }

            mat.SetTexture("_NoiseTexA", noiseA);
            mat.SetTexture("_NoiseTexB", noiseB);
            mat.SetFloat("_SpeedScale", 0.55f);
            mat.SetFloat("_Density", 0.5f);
            mat.SetFloat("_SoftDepthFade", 1.8f);
            mat.SetFloat("_Tiling", 0.045f);
            mat.SetColor("_ColorNear", new Color(0.82f, 0.68f, 0.42f, 1f));
            mat.SetColor("_ColorFar", new Color(0.62f, 0.48f, 0.30f, 1f));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        const string DissolveMatPath = RootFolder + "/M_DirectionalDissolve.mat";

        [MenuItem("Sand/Setup Directional Dissolve Preview", priority = 2)]
        public static void SetupDissolvePreview()
        {
            EnsureFolders();
            EnsureCameraAndLight();
            EnsureWind();

            Texture2D noiseA = CreateOrLoadNoise(NoiseAPath, seed: 11, scale: 6f, octaves: 4);
            Material mat = CreateOrUpdateDissolveMaterial(noiseA);

            GameObject go = GameObject.Find("DissolveDemo");
            if (go == null)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "DissolveDemo";
            }

            go.transform.position = new Vector3(0f, 1.2f, 0f);
            go.transform.localScale = new Vector3(2.2f, 2.2f, 2.2f);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var controller = go.GetComponent<DirectionalDissolveController>();
            if (controller == null)
                controller = go.AddComponent<DirectionalDissolveController>();

            var so = new SerializedObject(controller);
            so.FindProperty("mode").enumValueIndex = (int)DirectionalDissolveController.DissolveMode.BlowAway;
            so.FindProperty("dissolveStrength").floatValue = 0.2f;
            so.FindProperty("animate").boolValue = true;
            so.FindProperty("animateFrom").floatValue = 0.05f;
            so.FindProperty("animateTo").floatValue = 0.95f;
            so.FindProperty("animateDuration").floatValue = 5f;
            so.FindProperty("animateLoop").boolValue = true;
            so.FindProperty("useRendererCenterAsOrigin").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            controller.Apply();
            Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog(
                "Directional Dissolve Preview",
                "Created DissolveDemo cube.\n\n" +
                "1. Play — cube dissolves along wind with glowing edge\n" +
                "2. Switch Mode Cover on controller to preview sand burial\n" +
                "3. WindSystem controls dissolve direction\n" +
                "4. Tune Dissolve Strength / Edge Width / Edge Color\n\n" +
                "Shader: Sand/DirectionalDissolve",
                "OK");
        }

        static Material CreateOrUpdateDissolveMaterial(Texture2D noise)
        {
            Shader shader = Shader.Find("Sand/DirectionalDissolve");
            if (shader == null)
                throw new System.Exception("Cannot find Shader: Sand/DirectionalDissolve");

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(DissolveMatPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, DissolveMatPath);
            }
            else
            {
                mat.shader = shader;
            }

            mat.SetTexture("_DissolveNoise", noise);
            mat.SetTexture("_SandMap", noise);
            mat.SetColor("_BaseColor", new Color(0.55f, 0.58f, 0.62f, 1f));
            mat.SetColor("_SandColor", new Color(0.78f, 0.60f, 0.32f, 1f));
            mat.SetColor("_EdgeColor", new Color(1f, 0.72f, 0.35f, 1f));
            mat.SetFloat("_DissolveStrength", 0.25f);
            mat.SetFloat("_EdgeWidth", 0.045f);
            mat.SetFloat("_EdgeIntensity", 2.5f);
            mat.SetFloat("_NoiseTiling", 0.18f);
            mat.SetFloat("_NoiseStrength", 0.55f);
            mat.SetFloat("_DissolveScale", 0.1f);
            mat.EnableKeyword("_DISSOLVEMODE_BLOWAWAY");
            mat.DisableKeyword("_DISSOLVEMODE_COVER");
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
#endif
