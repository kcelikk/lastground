using System.IO;
using LastGround.UI.Run;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LastGround.EditorTools.Setup
{
    /// <summary>Combat HUD widgets for the run scene (placeholder look; UI art arrives in M11).</summary>
    static partial class RunSceneBuilder
    {
        const string UiArtDir = "Assets/Art/UI";
        static readonly Color HealthRed = new Color(0.78f, 0.12f, 0.1f, 1f);

        static (RectTransform, RectTransform, Image) BuildStick(string name, Transform canvas)
        {
            Sprite knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            RectTransform stickBase = UiFactory.Rect(name, canvas);
            stickBase.anchorMin = stickBase.anchorMax = new Vector2(0.5f, 0.5f);
            stickBase.sizeDelta = new Vector2(260f, 260f);
            var baseImage = stickBase.gameObject.AddComponent<Image>();
            baseImage.sprite = knob;
            baseImage.color = new Color(1f, 1f, 1f, 0.15f);
            baseImage.raycastTarget = false;
            RectTransform stickKnob = UiFactory.Rect("Knob", stickBase);
            stickKnob.sizeDelta = new Vector2(110f, 110f);
            var knobImage = stickKnob.gameObject.AddComponent<Image>();
            knobImage.sprite = knob;
            knobImage.color = new Color(1f, 1f, 1f, 0.5f);
            knobImage.raycastTarget = false;
            return (stickBase, stickKnob, knobImage);
        }

        static (Image, TMP_Text) BuildHealthBar(RectTransform parent)
        {
            RectTransform frame = UiFactory.Rect("HealthBar", parent);
            UiFactory.Place(frame, new Vector2(0f, 1f), new Vector2(24f, -22f), new Vector2(460f, 36f));
            var background = frame.gameObject.AddComponent<Image>();
            background.color = new Color(0.05f, 0.05f, 0.06f, 0.8f);
            background.raycastTarget = false;

            RectTransform fillRect = UiFactory.Rect("Fill", frame);
            UiFactory.Stretch(fillRect);
            fillRect.offsetMin = new Vector2(3f, 3f);
            fillRect.offsetMax = new Vector2(-3f, -3f);
            var fill = fillRect.gameObject.AddComponent<Image>();
            fill.sprite = WhiteSprite();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 1f;
            fill.color = HealthRed;
            fill.raycastTarget = false;

            TMP_Text label = UiFactory.Label("Value", frame, null, 26, FontStyles.Bold, Color.white);
            UiFactory.Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(12f, 0f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            return (fill, label);
        }

        static Image BuildReloadRing(RectTransform parent)
        {
            RectTransform rect = UiFactory.Rect("ReloadRing", parent);
            UiFactory.Place(rect, new Vector2(0f, 1f), new Vector2(24f, -72f), new Vector2(40f, 40f));
            var ring = rect.gameObject.AddComponent<Image>();
            ring.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            ring.fillAmount = 0f;
            ring.color = new Color(1f, 0.8f, 0.3f, 0.9f);
            ring.raycastTarget = false;
            return ring;
        }

        static Image BuildVignette(Transform canvas)
        {
            RectTransform rect = UiFactory.Panel("HurtVignette", canvas);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = VignetteSprite();
            image.color = new Color(0.7f, 0f, 0f, 0f);
            image.raycastTarget = false;
            return image;
        }

        static (GameObject, TMP_Text, Image) BuildDeathOverlay(Transform canvas)
        {
            RectTransform band = UiFactory.Rect("DeathOverlay", canvas);
            band.anchorMin = new Vector2(0f, 0.5f);
            band.anchorMax = new Vector2(1f, 0.5f);
            band.sizeDelta = new Vector2(0f, 150f);
            var background = band.gameObject.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.6f);
            background.raycastTarget = false;
            TMP_Text label = UiFactory.Label("Label", band, null, 56, FontStyles.Bold, new Color(1f, 0.35f, 0.3f));
            UiFactory.Stretch(label.rectTransform);
            label.alignment = TextAlignmentOptions.Center;
            RectTransform ringRect = UiFactory.Rect("ReviveRing", band);
            ringRect.anchorMin = ringRect.anchorMax = new Vector2(0.5f, 0.5f);
            ringRect.anchoredPosition = new Vector2(-560f, 0f);
            ringRect.sizeDelta = new Vector2(110f, 110f);
            var ring = ringRect.gameObject.AddComponent<Image>();
            ring.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            ring.fillAmount = 0f;
            ring.color = new Color(0.4f, 1f, 0.5f, 0.9f);
            ring.raycastTarget = false;
            band.gameObject.SetActive(false);
            return (band.gameObject, label, ring);
        }

        static TeamPanel BuildTeamPanel(RectTransform parent)
        {
            const int Rows = 3;
            var rows = new GameObject[Rows];
            var swatches = new Image[Rows];
            var names = new TMP_Text[Rows];
            var fills = new Image[Rows];
            var states = new TMP_Text[Rows];
            for (int i = 0; i < Rows; i++)
            {
                RectTransform row = UiFactory.Rect("Teammate" + (i + 1), parent);
                UiFactory.Place(row, new Vector2(0f, 1f), new Vector2(24f, -126f - i * 42f), new Vector2(520f, 36f));
                var background = row.gameObject.AddComponent<Image>();
                background.color = new Color(0.05f, 0.05f, 0.06f, 0.6f);
                background.raycastTarget = false;

                RectTransform swatch = UiFactory.Rect("Swatch", row);
                UiFactory.Place(swatch, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(8f, 36f));
                swatches[i] = swatch.gameObject.AddComponent<Image>();
                swatches[i].raycastTarget = false;

                names[i] = UiFactory.Label("Name", row, null, 22, FontStyles.Bold, Color.white);
                UiFactory.Place(names[i].rectTransform, new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(170f, 34f));
                names[i].alignment = TextAlignmentOptions.MidlineLeft;
                names[i].overflowMode = TextOverflowModes.Ellipsis;

                RectTransform bar = UiFactory.Rect("Health", row);
                UiFactory.Place(bar, new Vector2(0f, 0.5f), new Vector2(192f, 0f), new Vector2(150f, 10f));
                var barBack = bar.gameObject.AddComponent<Image>();
                barBack.color = new Color(0f, 0f, 0f, 0.6f);
                barBack.raycastTarget = false;
                RectTransform fillRect = UiFactory.Rect("Fill", bar);
                UiFactory.Stretch(fillRect);
                fills[i] = fillRect.gameObject.AddComponent<Image>();
                fills[i].sprite = WhiteSprite();
                fills[i].type = Image.Type.Filled;
                fills[i].fillMethod = Image.FillMethod.Horizontal;
                fills[i].color = HealthRed;
                fills[i].raycastTarget = false;

                states[i] = UiFactory.Label("State", row, null, 20, FontStyles.Bold, new Color(1f, 0.45f, 0.35f));
                UiFactory.Place(states[i].rectTransform, new Vector2(0f, 0.5f), new Vector2(352f, 0f), new Vector2(165f, 34f));
                states[i].alignment = TextAlignmentOptions.MidlineLeft;
                rows[i] = row.gameObject;
            }
            var panel = parent.gameObject.AddComponent<TeamPanel>();
            UiFactory.AssignArray(panel, "_rows", rows);
            UiFactory.AssignArray(panel, "_swatches", swatches);
            UiFactory.AssignArray(panel, "_names", names);
            UiFactory.AssignArray(panel, "_healthFills", fills);
            UiFactory.AssignArray(panel, "_states", states);
            return panel;
        }

        static TeammateIndicators BuildIndicators(Transform canvas)
        {
            RectTransform layer = UiFactory.Panel("TeammateIndicators", canvas);
            var arrows = new RectTransform[3];
            var images = new Image[3];
            for (int i = 0; i < arrows.Length; i++)
            {
                arrows[i] = UiFactory.Rect("Arrow" + (i + 1), layer);
                arrows[i].anchorMin = arrows[i].anchorMax = new Vector2(0.5f, 0.5f);
                arrows[i].sizeDelta = new Vector2(56f, 56f);
                images[i] = arrows[i].gameObject.AddComponent<Image>();
                images[i].sprite = ArrowSprite();
                images[i].raycastTarget = false;
            }
            var indicators = layer.gameObject.AddComponent<TeammateIndicators>();
            UiFactory.AssignArray(indicators, "_arrows", arrows);
            UiFactory.AssignArray(indicators, "_images", images);
            return indicators;
        }

        static ResultsScreen BuildResults(Transform canvas)
        {
            RectTransform panel = UiFactory.Panel("Results", canvas);
            var background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(0.02f, 0.02f, 0.03f, 0.88f);
            TMP_Text title = UiFactory.Label("Title", panel, null, 84, FontStyles.Bold, new Color(1f, 0.35f, 0.3f));
            UiFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(1200f, 110f));
            title.alignment = TextAlignmentOptions.Center;
            TMP_Text stats = UiFactory.Label("Stats", panel, null, 42, FontStyles.Normal, Color.white);
            UiFactory.Place(stats.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(900f, 360f));
            stats.alignment = TextAlignmentOptions.Center;
            stats.lineSpacing = 18f;
            Button menu = UiFactory.Button("Menu", panel, "results.menu", new Vector2(420f, 100f), out _);
            UiFactory.Place((RectTransform)menu.transform, new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(420f, 100f));

            var results = canvas.gameObject.AddComponent<ResultsScreen>();
            UiFactory.Assign(results, "_panel", panel.gameObject);
            UiFactory.Assign(results, "_title", title);
            UiFactory.Assign(results, "_stats", stats);
            UiFactory.Assign(results, "_menuButton", menu);
            return results;
        }

        /// <summary>Upward-pointing triangle (rotated by the indicator towards the teammate).</summary>
        static Sprite ArrowSprite() => LoadOrCreateSprite("TeammateArrow.png", 64, (x, y, size) =>
        {
            float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
            float halfWidth = (1f - v) * 0.45f;
            bool inside = v > 0.08f && Mathf.Abs(u - 0.5f) < halfWidth;
            return new Color(1f, 1f, 1f, inside ? 1f : 0f);
        });

        static Sprite WhiteSprite() => LoadOrCreateSprite("White.png", 4, (x, y, size) => Color.white);

        /// <summary>Transparent centre, opaque edges (the colour comes from the Image tint).</summary>
        static Sprite VignetteSprite() => LoadOrCreateSprite("HurtVignette.png", 128, (x, y, size) =>
        {
            float dx = (x + 0.5f) / size * 2f - 1f;
            float dy = (y + 0.5f) / size * 2f - 1f;
            float edge = Mathf.Clamp01((Mathf.Sqrt(dx * dx + dy * dy) - 0.55f) / 0.6f);
            return new Color(1f, 1f, 1f, edge * edge);
        });

        static Sprite LoadOrCreateSprite(string file, int size, System.Func<int, int, int, Color> pixel)
        {
            string path = UiArtDir + "/" + file;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return sprite;

            ProjectSetup.EnsureFolder(UiArtDir);
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    texture.SetPixel(x, y, pixel(x, y, size));
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
