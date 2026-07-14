using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SandSystem.Editor
{
    public static class DesertEnvironmentSetup
    {
        [MenuItem("Sand/Setup Environment Decision UI", priority = 4)]
        public static void Setup()
        {
            var env = EnsureEnvironmentSystem();
            var world = EnsureWorldPlacement();
            EnsureEventSystem();
            var canvasGo = EnsureDecisionCanvas(env, world);

            Selection.activeGameObject = canvasGo;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            EditorUtility.DisplayDialog(
                "Environment Decision UI",
                "已创建 / 更新：\n" +
                "• EnvironmentSystem（决策 → E → 风/沙/热浪）\n" +
                "• WorldPlacement（种树 / 建厂 / 采矿）\n" +
                "• DecisionUI\n\n" +
                "Prefab：Resources/_Prefab/tree 与 factory\n" +
                "请确认场景里已有 WindSystem / AirborneSand / HeatSystem。",
                "OK");
        }

        [MenuItem("Sand/Setup World Placement", priority = 5)]
        public static void SetupWorldPlacementOnly()
        {
            var world = EnsureWorldPlacement();
            Selection.activeGameObject = world.gameObject;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorUtility.DisplayDialog(
                "World Placement",
                "已创建 WorldPlacement（Tree / Factory / Mining）。\n" +
                "范围默认跟 Terrain；可在 Inspector 调集群半径等。",
                "OK");
        }

        [MenuItem("Sand/Select Environment System", priority = 22)]
        public static void SelectEnvironmentSystem()
        {
            var env = Object.FindFirstObjectByType<DesertEnvironmentSystem>();
            if (env == null)
            {
                EditorUtility.DisplayDialog("Environment System", "未找到。请先运行 Sand / Setup Environment Decision UI。", "OK");
                return;
            }
            Selection.activeGameObject = env.gameObject;
        }

        static DesertEnvironmentSystem EnsureEnvironmentSystem()
        {
            var existing = Object.FindFirstObjectByType<DesertEnvironmentSystem>();
            DesertEnvironmentSystem env;
            if (existing != null)
            {
                env = existing;
            }
            else
            {
                var go = new GameObject("EnvironmentSystem");
                env = go.AddComponent<DesertEnvironmentSystem>();
            }

            // 应用当前默认：树上限 50000、最糟热浪 0.02
            var so = new SerializedObject(env);
            var config = so.FindProperty("config");
            if (config != null)
            {
                config.FindPropertyRelative("treeMax").intValue = 50000;
                config.FindPropertyRelative("badHeatStrength").floatValue = 0.02f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            return env;
        }

        static DesertPlacementWorld EnsureWorldPlacement()
        {
            var existing = Object.FindFirstObjectByType<DesertPlacementWorld>();
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject("WorldPlacement");
                go.AddComponent<DesertPlacementWorld>();
            }

            if (go.GetComponent<TreePlacementSystem>() == null)
                go.AddComponent<TreePlacementSystem>();
            if (go.GetComponent<FactoryPlacementSystem>() == null)
                go.AddComponent<FactoryPlacementSystem>();
            if (go.GetComponent<MiningSystem>() == null)
                go.AddComponent<MiningSystem>();

            return go.GetComponent<DesertPlacementWorld>();
        }

        static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }

        static GameObject EnsureDecisionCanvas(DesertEnvironmentSystem env, DesertPlacementWorld world)
        {
            var existingUi = Object.FindFirstObjectByType<DesertEnvironmentUI>();
            if (existingUi != null)
            {
                WireUiReferences(existingUi, env, world);
                return existingUi.gameObject;
            }

            var canvasGo = new GameObject("DecisionUI");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var panel = CreatePanel(canvasGo.transform, "Panel", new Vector2(420, 520), new Vector2(16, -16));
            var anchor = panel.GetComponent<RectTransform>();
            anchor.anchorMin = new Vector2(0f, 1f);
            anchor.anchorMax = new Vector2(0f, 1f);
            anchor.pivot = new Vector2(0f, 1f);
            anchor.anchoredPosition = new Vector2(24, -24);

            float y = -24f;
            var title = CreateText(panel.transform, "Title", "环境决策", 28, TextAnchor.MiddleLeft, y, 36);
            y -= 48f;

            var envLabel = CreateText(panel.transform, "EnvironmentLabel", "环境指数 E  0.50", 22, TextAnchor.MiddleLeft, y, 30);
            y -= 36f;
            var windHint = CreateText(panel.transform, "WindDirHint", "自动风向：开", 16, TextAnchor.MiddleLeft, y, 24);
            y -= 40f;

            var treeLabel = CreateText(panel.transform, "TreeLabel", "树木  0 / 50000", 20, TextAnchor.MiddleLeft, y, 28);
            y -= 34f;
            var (plantTree, removeTree) = CreatePlusMinusRow(panel.transform, "TreeRow", y, "种树 +", "移除 -");
            y -= 56f;

            var factoryLabel = CreateText(panel.transform, "FactoryLabel", "工厂  0 / 20", 20, TextAnchor.MiddleLeft, y, 28);
            y -= 34f;
            var (buildFactory, removeFactory) = CreatePlusMinusRow(panel.transform, "FactoryRow", y, "建厂 +", "拆除 -");
            y -= 56f;

            var miningLabel = CreateText(panel.transform, "MiningLabel", "开采  0 / 100", 20, TextAnchor.MiddleLeft, y, 28);
            y -= 34f;
            var (startMining, reduceMining) = CreatePlusMinusRow(panel.transform, "MiningRow", y, "开采 +", "减少 -");
            y -= 60f;

            var toggleGo = CreateToggle(panel.transform, "AutoWindToggle", "自动改变风向", y);
            var toggle = toggleGo.GetComponent<Toggle>();
            toggle.isOn = true;

            var ui = canvasGo.AddComponent<DesertEnvironmentUI>();
            WireUiReferences(ui, env, world);
            // labels / buttons already assigned below via SerializedObject after create —
            // WireUiReferences only sets systems; continue assigning UI widgets:

            var so = new SerializedObject(ui);
            so.FindProperty("environment").objectReferenceValue = env;
            so.FindProperty("treePlacement").objectReferenceValue = world != null ? world.GetComponent<TreePlacementSystem>() : null;
            so.FindProperty("factoryPlacement").objectReferenceValue = world != null ? world.GetComponent<FactoryPlacementSystem>() : null;
            so.FindProperty("miningSystem").objectReferenceValue = world != null ? world.GetComponent<MiningSystem>() : null;
            so.FindProperty("treeLabel").objectReferenceValue = treeLabel;
            so.FindProperty("factoryLabel").objectReferenceValue = factoryLabel;
            so.FindProperty("miningLabel").objectReferenceValue = miningLabel;
            so.FindProperty("environmentLabel").objectReferenceValue = envLabel;
            so.FindProperty("windDirHintLabel").objectReferenceValue = windHint;
            so.FindProperty("plantTreeButton").objectReferenceValue = plantTree;
            so.FindProperty("removeTreeButton").objectReferenceValue = removeTree;
            so.FindProperty("buildFactoryButton").objectReferenceValue = buildFactory;
            so.FindProperty("removeFactoryButton").objectReferenceValue = removeFactory;
            so.FindProperty("startMiningButton").objectReferenceValue = startMining;
            so.FindProperty("reduceMiningButton").objectReferenceValue = reduceMining;
            so.FindProperty("autoWindDirectionToggle").objectReferenceValue = toggle;
            so.FindProperty("treeStep").intValue = 10;
            so.ApplyModifiedPropertiesWithoutUndo();

            return canvasGo;
        }

        static void WireUiReferences(DesertEnvironmentUI ui, DesertEnvironmentSystem env, DesertPlacementWorld world)
        {
            var so = new SerializedObject(ui);
            so.FindProperty("environment").objectReferenceValue = env;
            if (world != null)
            {
                so.FindProperty("treePlacement").objectReferenceValue = world.GetComponent<TreePlacementSystem>();
                so.FindProperty("factoryPlacement").objectReferenceValue = world.GetComponent<FactoryPlacementSystem>();
                so.FindProperty("miningSystem").objectReferenceValue = world.GetComponent<MiningSystem>();
            }
            so.FindProperty("treeStep").intValue = 10;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static GameObject CreatePanel(Transform parent, string name, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var img = go.GetComponent<Image>();
            img.color = new Color(0.08f, 0.09f, 0.1f, 0.82f);
            return go;
        }

        static Text CreateText(Transform parent, string name, string content, int fontSize, TextAnchor align, float y, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(-32f, height);
            rt.offsetMin = new Vector2(16f, rt.offsetMin.y);
            rt.offsetMax = new Vector2(-16f, rt.offsetMax.y);

            var text = go.GetComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null)
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = fontSize;
            text.alignment = align;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        static (Button plus, Button minus) CreatePlusMinusRow(Transform parent, string rowName, float y, string plusLabel, string minusLabel)
        {
            var row = new GameObject(rowName, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            var rt = row.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(-32f, 44f);
            rt.offsetMin = new Vector2(16f, rt.offsetMin.y);
            rt.offsetMax = new Vector2(-16f, rt.offsetMax.y);

            var plus = CreateButton(row.transform, "Plus", plusLabel, new Vector2(0f, 0.5f), new Vector2(0.48f, 0.5f), new Vector2(0f, 0.5f));
            var minus = CreateButton(row.transform, "Minus", minusLabel, new Vector2(0.52f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            return (plus, minus);
        }

        static Button CreateButton(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(anchorMin.x, 0f);
            rt.anchorMax = new Vector2(anchorMax.x, 1f);
            rt.pivot = pivot;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = go.GetComponent<Image>();
            img.color = new Color(0.22f, 0.45f, 0.38f, 1f);

            var btn = go.GetComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.3f, 0.55f, 0.45f, 1f);
            colors.pressedColor = new Color(0.15f, 0.35f, 0.28f, 1f);
            btn.colors = colors;

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            var text = textGo.GetComponent<Text>();
            text.text = label;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null)
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;

            return btn;
        }

        static GameObject CreateToggle(Transform parent, string name, string label, float y)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Toggle));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(-32f, 32f);
            rt.offsetMin = new Vector2(16f, rt.offsetMin.y);
            rt.offsetMax = new Vector2(-16f, rt.offsetMax.y);

            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(go.transform, false);
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 0.5f);
            bgRt.anchorMax = new Vector2(0f, 0.5f);
            bgRt.pivot = new Vector2(0f, 0.5f);
            bgRt.sizeDelta = new Vector2(28f, 28f);
            bgRt.anchoredPosition = Vector2.zero;
            bg.GetComponent<Image>().color = new Color(0.2f, 0.22f, 0.24f, 1f);

            var check = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            check.transform.SetParent(bg.transform, false);
            var ckRt = check.GetComponent<RectTransform>();
            ckRt.anchorMin = Vector2.zero;
            ckRt.anchorMax = Vector2.one;
            ckRt.offsetMin = new Vector2(4f, 4f);
            ckRt.offsetMax = new Vector2(-4f, -4f);
            check.GetComponent<Image>().color = new Color(0.4f, 0.85f, 0.55f, 1f);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var lrt = labelGo.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(40f, 0f);
            lrt.offsetMax = Vector2.zero;
            var text = labelGo.GetComponent<Text>();
            text.text = label;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null)
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = Color.white;

            var toggle = go.GetComponent<Toggle>();
            toggle.targetGraphic = bg.GetComponent<Image>();
            toggle.graphic = check.GetComponent<Image>();
            return go;
        }
    }
}
