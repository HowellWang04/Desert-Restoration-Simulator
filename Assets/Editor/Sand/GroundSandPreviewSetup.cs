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
            BuildTerrainGrid(mat, 3, restitchHeights: true);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Ground Sand Terrain Preview",
                "已生成 3×3（共 9 块）Terrain 流沙预览（约 180×180 米）。\n\n" +
                "1. 按 Play 查看近/中/远三层 Parallax 流沙\n" +
                "2. 用 Terrain 工具 Raise/Lower 雕刻高度\n" +
                "3. 选中 WindSystem 调节风向风速\n" +
                "4. 建议再运行 Sand / Setup Desert Skybox\n\n" +
                "资源：Assets/SandPreview/",
                "OK");
        }

        [MenuItem("Sand/Expand Terrains to 3x3 (9 tiles)", priority = 12)]
        public static void ExpandTerrainsTo3x3()
        {
            EnsureFolders();
            Texture2D noiseA = CreateOrLoadNoise(NoiseAPath, seed: 11, scale: 6f, octaves: 4);
            Texture2D noiseB = CreateOrLoadNoise(NoiseBPath, seed: 29, scale: 11f, octaves: 5);
            Material mat = CreateOrUpdateTerrainMaterial(noiseA, noiseB);
            BuildTerrainGrid(mat, 3, restitchHeights: true);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Terrains 3×3", "已确保场景中有 9 块相邻 Terrain，并用连续高度衔接接缝。", "OK");
        }

        [MenuItem("Sand/Restitch Terrain Heights (seamless)", priority = 13)]
        public static void RestitchTerrainHeightsMenu()
        {
            int n = RestitchAllSandTerrains();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog(
                "Restitch Heights",
                n > 0
                    ? $"已用世界坐标连续噪声重算 {n} 块地形高度，并强制对齐邻边接缝。"
                    : "场景中未找到 SandTerrain_* 。请先运行 Expand Terrains to 3x3。",
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

        static void BuildTerrainGrid(Material mat, int gridSize, bool restitchHeights = true)
        {
            EnsureCameraAndLight();
            EnsureWind();

            var oldPlane = GameObject.Find("SandGround");
            if (oldPlane != null)
                Object.DestroyImmediate(oldPlane);

            var legacy = GameObject.Find("SandTerrain");
            if (legacy != null)
                legacy.name = "SandTerrain_1_1";

            const float tile = 60f;
            const int sharedRes = 257;
            Vector3 origin = new Vector3(-30f, 0f, -30f);
            int center = gridSize / 2;

            Terrain[,] grid = new Terrain[gridSize, gridSize];

            for (int iz = 0; iz < gridSize; iz++)
            for (int ix = 0; ix < gridSize; ix++)
            {
                string name = $"SandTerrain_{ix}_{iz}";
                string dataPath = $"{RootFolder}/TD_Sand_{ix}_{iz}.asset";
                if (ix == center && iz == center)
                    dataPath = TerrainDataPath;

                Vector3 pos = origin + new Vector3((ix - center) * tile, 0f, (iz - center) * tile);

                TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);
                if (data == null)
                {
                    data = new TerrainData();
                    data.heightmapResolution = sharedRes;
                    data.size = new Vector3(tile, 12f, tile);
                    StampWorldSpaceDunes(data, pos);
                    AssetDatabase.CreateAsset(data, dataPath);
                }
                else
                {
                    if (data.heightmapResolution != sharedRes)
                        data.heightmapResolution = sharedRes;
                    data.size = new Vector3(tile, data.size.y > 0.1f ? data.size.y : 12f, tile);
                    if (restitchHeights)
                        StampWorldSpaceDunes(data, pos);
                    EditorUtility.SetDirty(data);
                }

                GameObject terrainGo = GameObject.Find(name);
                Terrain terrain;
                if (terrainGo == null)
                {
                    terrainGo = Terrain.CreateTerrainGameObject(data);
                    terrainGo.name = name;
                    terrain = terrainGo.GetComponent<Terrain>();
                }
                else
                {
                    terrain = terrainGo.GetComponent<Terrain>();
                    terrain.terrainData = data;
                    var col = terrainGo.GetComponent<TerrainCollider>();
                    if (col != null) col.terrainData = data;
                }

                terrainGo.transform.position = pos;
                terrain.materialTemplate = mat;
                terrain.drawInstanced = true;
                terrain.allowAutoConnect = true;

                if (terrainGo.GetComponent<GroundSandController>() == null)
                    terrainGo.AddComponent<GroundSandController>();

                grid[ix, iz] = terrain;
            }

            if (restitchHeights)
                StitchTerrainGridEdges(grid, gridSize);

            for (int iz = 0; iz < gridSize; iz++)
            for (int ix = 0; ix < gridSize; ix++)
            {
                Terrain left = ix > 0 ? grid[ix - 1, iz] : null;
                Terrain right = ix < gridSize - 1 ? grid[ix + 1, iz] : null;
                Terrain bottom = iz > 0 ? grid[ix, iz - 1] : null;
                Terrain top = iz < gridSize - 1 ? grid[ix, iz + 1] : null;
                grid[ix, iz].SetNeighbors(left, top, right, bottom);
            }

            var centerGo = grid[center, center] != null ? grid[center, center].gameObject : null;
            Selection.activeGameObject = centerGo;
            if (SceneView.lastActiveSceneView != null && centerGo != null)
            {
                SceneView.lastActiveSceneView.LookAt(
                    centerGo.transform.position + new Vector3(tile * 0.5f, 0f, tile * 0.5f),
                    Quaternion.Euler(40f, 0f, 0f),
                    tile * gridSize * 0.55f);
            }
        }

        static int RestitchAllSandTerrains()
        {
            const int gridSize = 3;
            Terrain[,] grid = new Terrain[gridSize, gridSize];
            int found = 0;

            for (int iz = 0; iz < gridSize; iz++)
            for (int ix = 0; ix < gridSize; ix++)
            {
                var go = GameObject.Find($"SandTerrain_{ix}_{iz}");
                if (go == null) continue;
                var terrain = go.GetComponent<Terrain>();
                if (terrain == null || terrain.terrainData == null) continue;

                var data = terrain.terrainData;
                const int sharedRes = 257;
                if (data.heightmapResolution != sharedRes)
                    data.heightmapResolution = sharedRes;

                StampWorldSpaceDunes(data, go.transform.position);
                EditorUtility.SetDirty(data);
                grid[ix, iz] = terrain;
                found++;
            }

            if (found == 0) return 0;

            StitchTerrainGridEdges(grid, gridSize);

            for (int iz = 0; iz < gridSize; iz++)
            for (int ix = 0; ix < gridSize; ix++)
            {
                if (grid[ix, iz] == null) continue;
                Terrain left = ix > 0 ? grid[ix - 1, iz] : null;
                Terrain right = ix < gridSize - 1 ? grid[ix + 1, iz] : null;
                Terrain bottom = iz > 0 ? grid[ix, iz - 1] : null;
                Terrain top = iz < gridSize - 1 ? grid[ix, iz + 1] : null;
                grid[ix, iz].SetNeighbors(left, top, right, bottom);
            }

            return found;
        }

        static void BuildTerrainPreview(Material mat)
        {
            BuildTerrainGrid(mat, 3, restitchHeights: true);
        }

        static void StampWorldSpaceDunes(TerrainData data, Vector3 worldOrigin)
        {
            int res = data.heightmapResolution;
            float sizeX = data.size.x;
            float sizeZ = data.size.z;
            float[,] heights = new float[res, res];

            for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                float wx = worldOrigin.x + (x / (float)(res - 1)) * sizeX;
                float wz = worldOrigin.z + (z / (float)(res - 1)) * sizeZ;
                heights[z, x] = SampleDuneHeight01(wx, wz);
            }

            data.SetHeights(0, 0, heights);
        }

        static float SampleDuneHeight01(float worldX, float worldZ)
        {
            float d1 = Mathf.PerlinNoise(worldX * 0.055f + 10.3f, worldZ * 0.055f + 4.7f);
            float d2 = Mathf.PerlinNoise(worldX * 0.12f + 40.1f, worldZ * 0.12f + 22.5f);
            float d3 = Mathf.PerlinNoise(worldX * 0.28f + 3.0f, worldZ * 0.28f + 8.0f);
            return Mathf.Clamp01(d1 * 0.48f + d2 * 0.28f + d3 * 0.12f + 0.06f);
        }

        static void StitchTerrainGridEdges(Terrain[,] grid, int gridSize)
        {
            for (int iz = 0; iz < gridSize; iz++)
            for (int ix = 0; ix < gridSize - 1; ix++)
            {
                var a = grid[ix, iz];
                var b = grid[ix + 1, iz];
                if (a == null || b == null) continue;
                StitchVerticalEdge(a.terrainData, b.terrainData);
                EditorUtility.SetDirty(a.terrainData);
                EditorUtility.SetDirty(b.terrainData);
            }

            for (int iz = 0; iz < gridSize - 1; iz++)
            for (int ix = 0; ix < gridSize; ix++)
            {
                var a = grid[ix, iz];
                var b = grid[ix, iz + 1];
                if (a == null || b == null) continue;
                StitchHorizontalEdge(a.terrainData, b.terrainData);
                EditorUtility.SetDirty(a.terrainData);
                EditorUtility.SetDirty(b.terrainData);
            }
        }

        static void StitchVerticalEdge(TerrainData left, TerrainData right)
        {
            int res = left.heightmapResolution;
            if (right.heightmapResolution != res)
                right.heightmapResolution = res;

            float[,] edgeL = left.GetHeights(res - 1, 0, 1, res);
            float[,] edgeR = right.GetHeights(0, 0, 1, res);
            float[,] outL = new float[res, 1];
            float[,] outR = new float[res, 1];
            for (int z = 0; z < res; z++)
            {
                float avg = (edgeL[z, 0] + edgeR[z, 0]) * 0.5f;
                outL[z, 0] = avg;
                outR[z, 0] = avg;
            }
            left.SetHeights(res - 1, 0, outL);
            right.SetHeights(0, 0, outR);
        }

        static void StitchHorizontalEdge(TerrainData south, TerrainData north)
        {
            int res = south.heightmapResolution;
            if (north.heightmapResolution != res)
                north.heightmapResolution = res;

            float[,] edgeS = south.GetHeights(0, res - 1, res, 1);
            float[,] edgeN = north.GetHeights(0, 0, res, 1);
            float[,] outS = new float[1, res];
            float[,] outN = new float[1, res];
            for (int x = 0; x < res; x++)
            {
                float avg = (edgeS[0, x] + edgeN[0, x]) * 0.5f;
                outS[0, x] = avg;
                outN[0, x] = avg;
            }
            south.SetHeights(0, res - 1, outS);
            north.SetHeights(0, 0, outN);
        }

        static void StampPreviewDunes(TerrainData data)
        {
            StampWorldSpaceDunes(data, Vector3.zero);
        }

        static void StampPreviewDunes(TerrainData data, int seedOffset)
        {
            StampWorldSpaceDunes(data, new Vector3(seedOffset * 60f, 0f, 0f));
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
