using LastGround.UI.Run;
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
