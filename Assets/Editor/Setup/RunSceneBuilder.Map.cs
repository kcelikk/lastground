using LastGround.UI.Run;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LastGround.EditorTools.Setup
{
    /// <summary>Round radar mini-map in the top-right corner, under the coin counter (reference boards).</summary>
    static partial class RunSceneBuilder
    {
        static MinimapHud BuildMinimap(RectTransform safe)
        {
            Sprite knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            RectTransform frame = UiFactory.Rect("Minimap", safe);
            UiFactory.Place(frame, new Vector2(1f, 1f), new Vector2(-24f, -262f), new Vector2(230f, 230f));
            var ring = frame.gameObject.AddComponent<Image>();
            ring.sprite = knob;
            ring.color = new Color(0.05f, 0.05f, 0.06f, 0.85f);
            ring.raycastTarget = false;

            // The circular mask clips the scrolled map and the dot overlay.
            RectTransform maskRect = UiFactory.Rect("Mask", frame);
            UiFactory.Stretch(maskRect);
            maskRect.offsetMin = new Vector2(6f, 6f);
            maskRect.offsetMax = new Vector2(-6f, -6f);
            var maskImage = maskRect.gameObject.AddComponent<Image>();
            maskImage.sprite = knob;
            maskImage.raycastTarget = false;
            maskRect.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            RawImage map = Raw("Map", maskRect, new Color(0.9f, 0.9f, 0.95f, 0.85f));
            RawImage dots = Raw("Dots", maskRect, Color.white);

            RectTransform arrow = UiFactory.Rect("Player", frame);
            arrow.anchorMin = arrow.anchorMax = new Vector2(0.5f, 0.5f);
            arrow.sizeDelta = new Vector2(22f, 22f);
            var arrowImage = arrow.gameObject.AddComponent<Image>();
            arrowImage.sprite = ArrowSprite();
            arrowImage.color = Color.white;
            arrowImage.raycastTarget = false;

            var hud = safe.gameObject.AddComponent<MinimapHud>();
            UiFactory.Assign(hud, "_map", map);
            UiFactory.Assign(hud, "_dots", dots);
            UiFactory.Assign(hud, "_arrow", arrow);
            return hud;
        }

        /// <summary>Region card under the status line: concept art (docs/reference/maps), name and danger level.</summary>
        static RegionCard BuildRegionCard(RectTransform safe)
        {
            RectTransform card = UiFactory.Rect("RegionCard", safe);
            UiFactory.Place(card, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(560f, 150f));
            var background = card.gameObject.AddComponent<Image>();
            background.color = UiFactory.PanelColor;
            background.raycastTarget = false;
            var group = card.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            RectTransform artRect = UiFactory.Rect("Art", card);
            artRect.anchorMin = artRect.anchorMax = new Vector2(0f, 0.5f);
            artRect.pivot = new Vector2(0f, 0.5f);
            artRect.anchoredPosition = new Vector2(8f, 0f);
            artRect.sizeDelta = new Vector2(201f, 134f);
            var art = artRect.gameObject.AddComponent<RawImage>();
            art.raycastTarget = false;

            TMP_Text name = UiFactory.Label("Name", card, null, 40, FontStyles.Bold, Color.white);
            UiFactory.Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(225f, 22f), new Vector2(325f, 56f));
            TMP_Text danger = UiFactory.Label("Danger", card, null, 26, FontStyles.Normal, UiFactory.Warning);
            UiFactory.Place(danger.rectTransform, new Vector2(0f, 0.5f), new Vector2(225f, -28f), new Vector2(325f, 40f));

            var region = card.gameObject.AddComponent<RegionCard>();
            UiFactory.Assign(region, "_group", group);
            UiFactory.Assign(region, "_art", art);
            UiFactory.Assign(region, "_name", name);
            UiFactory.Assign(region, "_danger", danger);
            return region;
        }

        static RawImage Raw(string name, RectTransform parent, Color color)
        {
            RectTransform rect = UiFactory.Rect(name, parent);
            UiFactory.Stretch(rect);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
    }
}
