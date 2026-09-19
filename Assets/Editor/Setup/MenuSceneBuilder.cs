using LastGround.UI.Common;
using LastGround.UI.Menu;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LastGround.EditorTools.Setup
{
    /// <summary>Menu scene: main panel, local co-op (find / join by IP / create) and lobby panels under one router.</summary>
    static class MenuSceneBuilder
    {
        public static void Build(string path)
        {
            Scene scene = SceneBuilder.NewScene("Assets/Scenes/Menu");
            SceneBuilder.CreateCamera(70f);
            SceneBuilder.CreateEventSystem();

            var canvasGo = new GameObject("Canvas_Menu", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
            var router = canvasGo.AddComponent<ScreenRouter>();

            BuildBackground(canvasGo.transform);
            RectTransform safe = UiFactory.Panel("SafeArea", canvasGo.transform);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            RectTransform main = UiFactory.Panel("MainPanel", safe);
            RectTransform coop = UiFactory.Panel("CoopPanel", safe);
            RectTransform lobby = UiFactory.Panel("LobbyPanel", safe);

            BuildMain(main, router, coop.gameObject);
            BuildCoop(coop, router, main.gameObject, lobby.gameObject);
            BuildLobby(lobby, router, coop.gameObject);

            UiFactory.AssignArray(router, "_screens", new Object[] { main.gameObject, coop.gameObject, lobby.gameObject });
            coop.gameObject.SetActive(false);
            lobby.gameObject.SetActive(false);
            EditorSceneManager.SaveScene(scene, path);
        }

        /// <summary>Darkened industrial district concept art behind the panels (D-020), cropped to fill the screen.</summary>
        static void BuildBackground(Transform canvas)
        {
            LastGround.EditorTools.Scenery.ConceptArtImport.Import();
            var texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(LastGround.EditorTools.Scenery.ConceptArtImport.MenuBackground);
            if (texture == null) return;
            RectTransform rect = UiFactory.Rect("Background", canvas);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.color = new Color(0.42f, 0.42f, 0.45f);
            image.raycastTarget = false;
            var fitter = rect.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = (float)texture.width / texture.height;
        }

        static void BuildMain(RectTransform panel, ScreenRouter router, GameObject coop)
        {
            TMP_Text title = UiFactory.Label("Title", panel, "menu.title", 120, FontStyles.Bold, Color.white);
            UiFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(80f, -80f), new Vector2(1100f, 150f));
            TMP_Text tagline = UiFactory.Label("Tagline", panel, "menu.tagline", 34, FontStyles.Normal, UiFactory.Accent);
            UiFactory.Place(tagline.rectTransform, new Vector2(0f, 1f), new Vector2(86f, -225f), new Vector2(1100f, 50f));

            RectTransform list = UiFactory.Rect("Buttons", panel);
            UiFactory.Place(list, new Vector2(0f, 1f), new Vector2(80f, -320f), new Vector2(560f, 520f));
            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16f;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;

            var size = new Vector2(560f, 84f); // ≥ 56 dp at the 1080p reference (TDD_01 §0.9)
            Button solo = UiFactory.Button("Solo", list, "menu.solo", size, out _);
            Button coopButton = UiFactory.Button("LocalCoop", list, "menu.local_coop", size, out _);
            Button settings = UiFactory.Button("Settings", list, "menu.settings", size, out _);
            Button language = UiFactory.Button("Language", list, null, size, out TMP_Text languageLabel);
            Button quit = UiFactory.Button("Quit", list, "menu.quit", size, out _);

            TMP_Text status = UiFactory.Label("Status", panel, null, 32, FontStyles.Normal, UiFactory.Warning);
            UiFactory.Place(status.rectTransform, new Vector2(0f, 0f), new Vector2(80f, 120f), new Vector2(1600f, 50f));
            TMP_Text version = UiFactory.Label("Version", panel, null, 24, FontStyles.Normal, UiFactory.Muted);
            UiFactory.Place(version.rectTransform, new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(400f, 36f));
            version.alignment = TextAlignmentOptions.BottomRight;

            var screen = panel.gameObject.AddComponent<MainMenuScreen>();
            UiFactory.Assign(screen, "_soloButton", solo);
            UiFactory.Assign(screen, "_coopButton", coopButton);
            UiFactory.Assign(screen, "_settingsButton", settings);
            UiFactory.Assign(screen, "_languageButton", language);
            UiFactory.Assign(screen, "_quitButton", quit);
            UiFactory.Assign(screen, "_languageLabel", languageLabel);
            UiFactory.Assign(screen, "_statusLabel", status);
            UiFactory.Assign(screen, "_versionLabel", version);
            UiFactory.Assign(screen, "_router", router);
            UiFactory.Assign(screen, "_coopScreen", coop);
        }

        static void BuildCoop(RectTransform panel, ScreenRouter router, GameObject main, GameObject lobby)
        {
            TMP_Text title = UiFactory.Label("Title", panel, "coop.title", 72, FontStyles.Bold, Color.white);
            UiFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(80f, -60f), new Vector2(1000f, 90f));
            TMP_Text header = UiFactory.Label("GamesHeader", panel, "coop.find_games", 30, FontStyles.Bold, UiFactory.Muted);
            UiFactory.Place(header.rectTransform, new Vector2(0f, 1f), new Vector2(84f, -170f), new Vector2(1000f, 40f));

            const int rows = 6;
            var rowButtons = new Object[rows];
            var rowLabels = new Object[rows];
            for (int i = 0; i < rows; i++)
            {
                Button row = UiFactory.Button("Host" + i, panel, null, new Vector2(1000f, 76f), out TMP_Text label, 34f);
                UiFactory.Place((RectTransform)row.transform, new Vector2(0f, 1f), new Vector2(80f, -220f - i * 88f), new Vector2(1000f, 76f));
                rowButtons[i] = row;
                rowLabels[i] = label;
            }

            TMP_InputField ip = UiFactory.InputField("IpInput", panel, "coop.ip_placeholder", new Vector2(600f, 84f));
            UiFactory.Place((RectTransform)ip.transform, new Vector2(1f, 1f), new Vector2(-80f, -220f), new Vector2(600f, 84f));
            Button joinIp = UiFactory.Button("JoinByIp", panel, "coop.join_by_ip", new Vector2(600f, 84f), out _);
            UiFactory.Place((RectTransform)joinIp.transform, new Vector2(1f, 1f), new Vector2(-80f, -320f), new Vector2(600f, 84f));
            Button create = UiFactory.Button("Create", panel, "coop.create", new Vector2(600f, 84f), out _);
            UiFactory.Place((RectTransform)create.transform, new Vector2(1f, 1f), new Vector2(-80f, -480f), new Vector2(600f, 84f));
            Button back = UiFactory.Button("Back", panel, "coop.back", new Vector2(300f, 84f), out _);
            UiFactory.Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(80f, 60f), new Vector2(300f, 84f));

            TMP_Text status = UiFactory.Label("Status", panel, null, 30, FontStyles.Normal, UiFactory.Warning);
            UiFactory.Place(status.rectTransform, new Vector2(0f, 0f), new Vector2(420f, 70f), new Vector2(1400f, 60f));
            status.textWrappingMode = TextWrappingModes.Normal;

            var screen = panel.gameObject.AddComponent<LocalCoopScreen>();
            UiFactory.Assign(screen, "_router", router);
            UiFactory.Assign(screen, "_mainScreen", main);
            UiFactory.Assign(screen, "_lobbyScreen", lobby);
            UiFactory.Assign(screen, "_createButton", create);
            UiFactory.Assign(screen, "_joinIpButton", joinIp);
            UiFactory.Assign(screen, "_backButton", back);
            UiFactory.Assign(screen, "_ipInput", ip);
            UiFactory.Assign(screen, "_statusLabel", status);
            UiFactory.AssignArray(screen, "_hostRows", rowButtons);
            UiFactory.AssignArray(screen, "_hostLabels", rowLabels);
        }

        static void BuildLobby(RectTransform panel, ScreenRouter router, GameObject coop)
        {
            TMP_Text title = UiFactory.Label("Title", panel, null, 64, FontStyles.Bold, Color.white);
            UiFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(80f, -60f), new Vector2(1400f, 90f));
            TMP_Text address = UiFactory.Label("Address", panel, null, 32, FontStyles.Normal, UiFactory.Accent);
            UiFactory.Place(address.rectTransform, new Vector2(0f, 1f), new Vector2(84f, -160f), new Vector2(1400f, 44f));

            const int rows = 4;
            var labels = new Object[rows];
            for (int i = 0; i < rows; i++)
            {
                RectTransform row = UiFactory.Rect("Player" + i, panel);
                UiFactory.Place(row, new Vector2(0f, 1f), new Vector2(80f, -240f - i * 96f), new Vector2(1000f, 84f));
                row.gameObject.AddComponent<Image>().color = UiFactory.PanelColor;
                TMP_Text label = UiFactory.Label("Label", row, null, 38, FontStyles.Bold, Color.white);
                UiFactory.Stretch(label.rectTransform);
                label.rectTransform.offsetMin = new Vector2(28f, 0f);
                label.alignment = TextAlignmentOptions.MidlineLeft;
                labels[i] = label;
            }

            Button start = UiFactory.Button("Start", panel, "lobby.start", new Vector2(420f, 96f), out _);
            UiFactory.Place((RectTransform)start.transform, new Vector2(1f, 0f), new Vector2(-80f, 60f), new Vector2(420f, 96f));
            start.GetComponent<Image>().color = UiFactory.Accent;
            Button leave = UiFactory.Button("Leave", panel, "lobby.leave", new Vector2(300f, 84f), out _);
            UiFactory.Place((RectTransform)leave.transform, new Vector2(0f, 0f), new Vector2(80f, 60f), new Vector2(300f, 84f));
            TMP_Text waiting = UiFactory.Label("Waiting", panel, "lobby.waiting_host", 32, FontStyles.Italic, UiFactory.Muted);
            UiFactory.Place(waiting.rectTransform, new Vector2(1f, 0f), new Vector2(-80f, 80f), new Vector2(900f, 50f));
            waiting.alignment = TextAlignmentOptions.BottomRight;

            var screen = panel.gameObject.AddComponent<LobbyScreen>();
            UiFactory.Assign(screen, "_router", router);
            UiFactory.Assign(screen, "_coopScreen", coop);
            UiFactory.Assign(screen, "_titleLabel", title);
            UiFactory.Assign(screen, "_addressLabel", address);
            UiFactory.Assign(screen, "_waitingLabel", waiting);
            UiFactory.AssignArray(screen, "_playerLabels", labels);
            UiFactory.Assign(screen, "_startButton", start);
            UiFactory.Assign(screen, "_leaveButton", leave);
        }
    }
}
