using LastGround.UI.Run;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastGround.EditorTools.Setup
{
    /// <summary>M9 HUD: emote button with its options under the radar, speech bubbles over players.</summary>
    static partial class RunSceneBuilder
    {
        static EmoteBar BuildEmoteBar(RectTransform safe)
        {
            RectTransform root = UiFactory.Rect("EmoteBar", safe);
            UiFactory.Place(root, new Vector2(1f, 1f), new Vector2(-24f, -510f), new Vector2(160f, 64f));
            Button toggle = UiFactory.Button("Toggle", root, "hud.emote", new Vector2(160f, 64f), out TMP_Text toggleLabel, 26f);
            toggleLabel.alignment = TextAlignmentOptions.Center;
            toggleLabel.rectTransform.offsetMin = Vector2.zero;
            UiFactory.Stretch((RectTransform)toggle.transform);

            RectTransform options = UiFactory.Rect("Options", root);
            UiFactory.Place(options, new Vector2(0f, 0.5f), new Vector2(-12f, 0f), new Vector2(3 * 170f, 64f));
            options.pivot = new Vector2(1f, 0.5f);
            var buttons = new Object[3];
            for (int i = 0; i < buttons.Length; i++)
            {
                Button option = UiFactory.Button("Emote" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), options, null,
                    new Vector2(160f, 64f), out TMP_Text label, 24f);
                label.alignment = TextAlignmentOptions.Center;
                label.rectTransform.offsetMin = Vector2.zero;
                UiFactory.Place((RectTransform)option.transform, new Vector2(0f, 0.5f), new Vector2(i * 170f, 0f), new Vector2(160f, 64f));
                buttons[i] = option;
            }

            var bar = root.gameObject.AddComponent<EmoteBar>();
            UiFactory.Assign(bar, "_toggle", toggle);
            UiFactory.Assign(bar, "_options", options.gameObject);
            UiFactory.AssignArray(bar, "_buttons", buttons);
            return bar;
        }

        static EmoteBubbles BuildEmoteBubbles(Transform canvas)
        {
            RectTransform layer = UiFactory.Panel("EmoteBubbles", canvas);
            var labels = new Object[4];
            for (int p = 0; p < labels.Length; p++)
            {
                TMP_Text label = UiFactory.Label("Bubble" + p.ToString(System.Globalization.CultureInfo.InvariantCulture), layer, null, 34,
                    FontStyles.Bold, Color.white);
                RectTransform rect = label.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.sizeDelta = new Vector2(360f, 56f);
                label.alignment = TextAlignmentOptions.Center;
                label.outlineWidth = 0.25f;
                labels[p] = label;
            }
            var bubbles = layer.gameObject.AddComponent<EmoteBubbles>();
            UiFactory.AssignArray(bubbles, "_labels", labels);
            return bubbles;
        }
    }
}
