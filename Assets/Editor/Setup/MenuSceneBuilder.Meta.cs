using LastGround.UI.Common;
using LastGround.UI.Menu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastGround.EditorTools.Setup
{
    /// <summary>M9 preparation screen: tab row, scrolling item list with a row template, Scrap and statistics column.</summary>
    static partial class MenuSceneBuilder
    {
        const int MetaTabs = 7;

        static void BuildMeta(RectTransform panel, ScreenRouter router, GameObject main)
        {
            TMP_Text title = UiFactory.Label("Title", panel, "meta.title", 72, FontStyles.Bold, Color.white);
            UiFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(80f, -50f), new Vector2(1000f, 90f));

            var tabs = new Object[MetaTabs];
            for (int i = 0; i < MetaTabs; i++)
            {
                Button tab = UiFactory.Button("Tab" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), panel, null,
                    new Vector2(222f, 64f), out TMP_Text label, 26f);
                label.alignment = TextAlignmentOptions.Center;
                label.rectTransform.offsetMin = Vector2.zero;
                UiFactory.Place((RectTransform)tab.transform, new Vector2(0f, 1f), new Vector2(80f + i * 230f, -150f), new Vector2(222f, 64f));
                tabs[i] = tab;
            }

            // Scrolling list: viewport with a mask, content grows with its rows.
            RectTransform viewport = UiFactory.Rect("List", panel);
            UiFactory.Place(viewport, new Vector2(0f, 1f), new Vector2(80f, -236f), new Vector2(1160f, 700f));
            viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = UiFactory.Rect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 0f);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            GameObject template = Row(content);

            TMP_Text scrap = UiFactory.Label("Scrap", panel, null, 44, FontStyles.Bold, new Color(1f, 0.8f, 0.25f));
            UiFactory.Place(scrap.rectTransform, new Vector2(1f, 1f), new Vector2(-80f, -236f), new Vector2(560f, 60f));
            scrap.alignment = TextAlignmentOptions.Right;
            TMP_Text stats = UiFactory.Label("Stats", panel, null, 28, FontStyles.Normal, Color.white);
            UiFactory.Place(stats.rectTransform, new Vector2(1f, 1f), new Vector2(-80f, -310f), new Vector2(560f, 420f));
            stats.alignment = TextAlignmentOptions.TopRight;
            stats.textWrappingMode = TextWrappingModes.Normal;
            TMP_Text status = UiFactory.Label("Status", panel, null, 30, FontStyles.Normal, UiFactory.Warning);
            UiFactory.Place(status.rectTransform, new Vector2(1f, 0f), new Vector2(-80f, 150f), new Vector2(560f, 60f));
            status.alignment = TextAlignmentOptions.Right;
            Button back = UiFactory.Button("Back", panel, "coop.back", new Vector2(300f, 84f), out _);
            UiFactory.Place((RectTransform)back.transform, new Vector2(1f, 0f), new Vector2(-80f, 50f), new Vector2(300f, 84f));

            var screen = panel.gameObject.AddComponent<MetaScreen>();
            UiFactory.AssignArray(screen, "_tabButtons", tabs);
            UiFactory.Assign(screen, "_list", content);
            UiFactory.Assign(screen, "_rowTemplate", template);
            UiFactory.Assign(screen, "_scrapLabel", scrap);
            UiFactory.Assign(screen, "_statsLabel", stats);
            UiFactory.Assign(screen, "_statusLabel", status);
            UiFactory.Assign(screen, "_backButton", back);
            UiFactory.Assign(screen, "_router", router);
            UiFactory.Assign(screen, "_mainScreen", main);
        }

        /// <summary>Row template: name and description on the left, one action button on the right.</summary>
        static GameObject Row(RectTransform parent)
        {
            RectTransform row = UiFactory.Rect("RowTemplate", parent);
            row.sizeDelta = new Vector2(0f, 116f);
            row.gameObject.AddComponent<Image>().color = UiFactory.PanelColor;
            TMP_Text name = UiFactory.Label("Name", row, null, 36, FontStyles.Bold, Color.white);
            UiFactory.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -10f), new Vector2(760f, 48f));
            TMP_Text description = UiFactory.Label("Description", row, null, 26, FontStyles.Normal, UiFactory.Muted);
            UiFactory.Place(description.rectTransform, new Vector2(0f, 0f), new Vector2(24f, 10f), new Vector2(760f, 50f));
            description.textWrappingMode = TextWrappingModes.Normal;
            Button action = UiFactory.Button("Action", row, null, new Vector2(300f, 80f), out TMP_Text label, 30f);
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.offsetMin = Vector2.zero;
            UiFactory.Place((RectTransform)action.transform, new Vector2(1f, 0.5f), new Vector2(-18f, 0f), new Vector2(300f, 80f));
            return row.gameObject;
        }
    }
}
