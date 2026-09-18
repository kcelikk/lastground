using System.IO;
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

        static (GameObject, TMP_Text) BuildDeathOverlay(Transform canvas)
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
            band.gameObject.SetActive(false);
            return (band.gameObject, label);
        }

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
