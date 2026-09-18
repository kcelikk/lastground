using LastGround.App;
using LastGround.Localization;
using LastGround.UI.Common;
using LastGround.UI.Menu;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LastGround.EditorTools.Setup
{
    /// <summary>
    /// Generates the Boot and Menu scenes (TDD_02 §28) and registers them in Build Settings.
    /// Existing scenes are left untouched so manual edits are never overwritten; delete a scene to regenerate it.
    /// </summary>
    static class SceneBuilder
    {
        public const string BootPath = "Assets/Scenes/Boot/Boot.unity";
        public const string MenuPath = "Assets/Scenes/Menu/Menu.unity";

        static readonly Color Background = new Color(0.043f, 0.051f, 0.063f);
        static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.11f, 0.88f);
        static readonly Color Accent = new Color(0.78f, 0.12f, 0.1f);

        public static void BuildAll()
        {
            if (!System.IO.File.Exists(BootPath)) BuildBoot();
            if (!System.IO.File.Exists(MenuPath)) BuildMenu();

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootPath, true),
                new EditorBuildSettingsScene(MenuPath, true),
            };
        }

        static void BuildBoot()
        {
            ProjectSetup.EnsureFolder("Assets/Scenes/Boot");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();
            new GameObject("Boot", typeof(BootLoader));
            EditorSceneManager.SaveScene(scene, BootPath);
        }

        static void BuildMenu()
        {
            ProjectSetup.EnsureFolder("Assets/Scenes/Menu");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var canvasGo = new GameObject("Canvas_Menu", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;

            RectTransform safe = Rect("SafeArea", canvasGo.transform);
            Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            TMP_Text title = Label("Title", safe, "menu.title", 120, FontStyles.Bold, Color.white);
            Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(80f, -80f), new Vector2(1100f, 150f));
            TMP_Text tagline = Label("Tagline", safe, "menu.tagline", 34, FontStyles.Normal, Accent);
            Place(tagline.rectTransform, new Vector2(0f, 1f), new Vector2(86f, -225f), new Vector2(1100f, 50f));

            RectTransform list = Rect("Buttons", safe);
            Place(list, new Vector2(0f, 1f), new Vector2(80f, -320f), new Vector2(560f, 520f));
            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16f;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;

            Button solo = MenuButton("Solo", list, "menu.solo", out _);
            Button coop = MenuButton("LocalCoop", list, "menu.local_coop", out _);
            Button settings = MenuButton("Settings", list, "menu.settings", out _);
            Button language = MenuButton("Language", list, null, out TMP_Text languageLabel);
            Button quit = MenuButton("Quit", list, "menu.quit", out _);

            TMP_Text status = Label("Status", safe, null, 32, FontStyles.Normal, new Color(1f, 0.8f, 0.3f));
            Place(status.rectTransform, new Vector2(0f, 0f), new Vector2(80f, 120f), new Vector2(1200f, 50f));
            TMP_Text version = Label("Version", safe, null, 24, FontStyles.Normal, new Color(1f, 1f, 1f, 0.5f));
            Place(version.rectTransform, new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(400f, 36f));
            version.alignment = TextAlignmentOptions.BottomRight;

            var screen = canvasGo.AddComponent<MainMenuScreen>();
            var so = new SerializedObject(screen);
            so.FindProperty("_soloButton").objectReferenceValue = solo;
            so.FindProperty("_coopButton").objectReferenceValue = coop;
            so.FindProperty("_settingsButton").objectReferenceValue = settings;
            so.FindProperty("_languageButton").objectReferenceValue = language;
            so.FindProperty("_quitButton").objectReferenceValue = quit;
            so.FindProperty("_languageLabel").objectReferenceValue = languageLabel;
            so.FindProperty("_statusLabel").objectReferenceValue = status;
            so.FindProperty("_versionLabel").objectReferenceValue = version;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, MenuPath);
        }

        static void CreateCamera()
        {
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";
            var camera = go.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            camera.farClipPlane = 70f;
        }

        static Button MenuButton(string name, Transform parent, string key, out TMP_Text label)
        {
            RectTransform rect = Rect(name, parent);
            rect.sizeDelta = new Vector2(560f, 84f); // ≥ 56 dp at 1080p reference (TDD_01 §0.9)
            var image = rect.gameObject.AddComponent<Image>();
            image.color = PanelColor;
            var button = rect.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.8f, 0.3f, 0.25f, 1f);
            button.colors = colors;

            label = Label("Label", rect, key, 38, FontStyles.Bold, Color.white);
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(28f, 0f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            return button;
        }

        static TMP_Text Label(string name, Transform parent, string key, float size, FontStyles style, Color color)
        {
            RectTransform rect = Rect(name, parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            if (!string.IsNullOrEmpty(key))
            {
                var localized = rect.gameObject.AddComponent<LocalizedText>();
                var so = new SerializedObject(localized);
                so.FindProperty("_key").stringValue = key;
                so.ApplyModifiedPropertiesWithoutUndo();
                text.text = key;
            }
            return text;
        }

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
