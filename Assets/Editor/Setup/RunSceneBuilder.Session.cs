using LastGround.UI.Run;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastGround.EditorTools.Setup
{
    /// <summary>M10 HUD: the spectator bar (watched teammate + next) under the death banner while dead.</summary>
    static partial class RunSceneBuilder
    {
        static SpectatorBar BuildSpectatorBar(RectTransform safe)
        {
            RectTransform root = UiFactory.Rect("SpectatorBar", safe);
            // Under the "dead — back in" banner: the bottom centre belongs to the level-up panel.
            UiFactory.Place(root, new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(640f, 72f));
            RectTransform panel = UiFactory.Rect("Panel", root);
            UiFactory.Stretch(panel);
            var background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(0.05f, 0.05f, 0.06f, 0.7f);
            background.raycastTarget = false;

            TMP_Text label = UiFactory.Label("Watching", panel, null, 28, FontStyles.Bold, Color.white);
            UiFactory.Place(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(420f, 64f));
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.overflowMode = TextOverflowModes.Ellipsis;

            Button next = UiFactory.Button("Next", panel, "hud.spectate.next", new Vector2(180f, 60f), out TMP_Text nextLabel, 26f);
            nextLabel.alignment = TextAlignmentOptions.Center;
            nextLabel.rectTransform.offsetMin = Vector2.zero;
            UiFactory.Place((RectTransform)next.transform, new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(180f, 60f));

            var bar = root.gameObject.AddComponent<SpectatorBar>();
            UiFactory.Assign(bar, "_panel", panel.gameObject);
            UiFactory.Assign(bar, "_label", label);
            UiFactory.Assign(bar, "_next", next);
            return bar;
        }
    }
}
