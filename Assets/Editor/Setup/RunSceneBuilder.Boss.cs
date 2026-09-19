using LastGround.UI.Run;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LastGround.EditorTools.Setup
{
    /// <summary>M8 HUD: boss bar and extraction line at the top centre, extraction edge arrow, horde ring on the radar.</summary>
    static partial class RunSceneBuilder
    {
        static readonly Color ExtractionBlue = new Color(0.45f, 0.75f, 1f);

        /// <summary>Boss name and health bar under the extraction line, with phase marks (TDD_01 §0.9 panel 7).</summary>
        static BossHealthBar BuildBossBar(RectTransform safe)
        {
            RectTransform root = UiFactory.Rect("BossBar", safe);
            UiFactory.Place(root, new Vector2(0.5f, 1f), new Vector2(0f, -168f), new Vector2(760f, 74f));
            TMP_Text name = UiFactory.Label("Name", root, null, 30, FontStyles.Bold, new Color(1f, 0.88f, 0.8f));
            UiFactory.Place(name.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(760f, 38f));
            name.alignment = TextAlignmentOptions.Center;

            RectTransform frame = UiFactory.Rect("Frame", root);
            UiFactory.Place(frame, new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(740f, 24f));
            var back = frame.gameObject.AddComponent<Image>();
            back.color = new Color(0f, 0f, 0f, 0.7f);
            back.raycastTarget = false;
            RectTransform fillRect = UiFactory.Rect("Fill", frame);
            UiFactory.Stretch(fillRect);
            fillRect.offsetMin = new Vector2(3f, 3f);
            fillRect.offsetMax = new Vector2(-3f, -3f);
            var fill = fillRect.gameObject.AddComponent<Image>();
            fill.color = new Color(0.62f, 0.08f, 0.06f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.raycastTarget = false;
            RectTransform phase2 = Mark("Phase2Mark", fillRect);
            RectTransform enraged = Mark("EnragedMark", fillRect);

            var bar = root.gameObject.AddComponent<BossHealthBar>();
            UiFactory.Assign(bar, "_root", root.gameObject);
            UiFactory.Assign(bar, "_name", name);
            UiFactory.Assign(bar, "_fill", fill);
            UiFactory.Assign(bar, "_phase2Mark", phase2);
            UiFactory.Assign(bar, "_enragedMark", enraged);
            return bar;
        }

        static RectTransform Mark(string name, RectTransform parent)
        {
            RectTransform mark = UiFactory.Rect(name, parent);
            mark.pivot = new Vector2(0.5f, 0.5f);
            mark.sizeDelta = new Vector2(3f, 0f);
            var image = mark.gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.6f);
            image.raycastTarget = false;
            return mark;
        }

        /// <summary>Extraction line + hold bar under the status line, and a blue edge arrow to the landing zone.</summary>
        static ExtractionHud BuildExtractionHud(RectTransform safe, Transform canvas)
        {
            RectTransform root = UiFactory.Rect("Extraction", safe);
            UiFactory.Place(root, new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(1000f, 52f));
            TMP_Text line = UiFactory.Label("Line", root, null, 32, FontStyles.Bold, ExtractionBlue);
            UiFactory.Place(line.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1000f, 40f));
            line.alignment = TextAlignmentOptions.Center;
            RectTransform barRect = UiFactory.Rect("Hold", root);
            UiFactory.Place(barRect, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(460f, 8f));
            var bar = barRect.gameObject.AddComponent<Image>();
            bar.color = ExtractionBlue;
            bar.type = Image.Type.Filled;
            bar.fillMethod = Image.FillMethod.Horizontal;
            bar.raycastTarget = false;

            RectTransform layer = UiFactory.Panel("ExtractionIndicator", canvas);
            RectTransform arrow = UiFactory.Rect("Arrow", layer);
            arrow.anchorMin = arrow.anchorMax = new Vector2(0.5f, 0.5f);
            arrow.sizeDelta = new Vector2(70f, 70f);
            var arrowImage = arrow.gameObject.AddComponent<Image>();
            arrowImage.sprite = ArrowSprite();
            arrowImage.color = new Color(ExtractionBlue.r, ExtractionBlue.g, ExtractionBlue.b, 0.95f);
            arrowImage.raycastTarget = false;

            var hud = root.gameObject.AddComponent<ExtractionHud>();
            UiFactory.Assign(hud, "_root", line.transform.parent.gameObject);
            UiFactory.Assign(hud, "_line", line);
            UiFactory.Assign(hud, "_progress", bar);
            UiFactory.Assign(hud, "_arrow", arrow);
            UiFactory.Assign(hud, "_arrowImage", arrowImage);
            return hud;
        }

        /// <summary>12 segments around the radar, lit towards far hordes (TDD_01 §9.11).</summary>
        static Image[] BuildHordeRing(RectTransform radar)
        {
            Sprite knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            var sectors = new Image[12];
            for (int s = 0; s < sectors.Length; s++)
            {
                RectTransform rect = UiFactory.Rect("Horde" + s.ToString(System.Globalization.CultureInfo.InvariantCulture), radar);
                UiFactory.Stretch(rect);
                rect.offsetMin = new Vector2(-10f, -10f);
                rect.offsetMax = new Vector2(10f, 10f);
                // Sector s is centred on s × 30° (0 = east, counter-clockwise), 24° wide.
                rect.localRotation = Quaternion.Euler(0f, 0f, s * 30f - 12f);
                var image = rect.gameObject.AddComponent<Image>();
                image.sprite = knob;
                image.type = Image.Type.Filled;
                image.fillMethod = Image.FillMethod.Radial360;
                image.fillOrigin = (int)Image.Origin360.Right;
                image.fillClockwise = false;
                image.fillAmount = 24f / 360f;
                image.color = new Color(0.95f, 0.15f, 0.1f, 0f);
                image.raycastTarget = false;
                image.transform.SetAsFirstSibling();
                sectors[s] = image;
            }
            return sectors;
        }
    }
}
