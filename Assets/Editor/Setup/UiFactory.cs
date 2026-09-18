using LastGround.Localization;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LastGround.EditorTools.Setup
{
    /// <summary>Small helpers to build uGUI hierarchies from editor scripts (no layout groups in HUDs).</summary>
    static class UiFactory
    {
        public static readonly Color Background = new Color(0.043f, 0.051f, 0.063f);
        public static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.11f, 0.88f);
        public static readonly Color Accent = new Color(0.78f, 0.12f, 0.1f);
        public static readonly Color Muted = new Color(1f, 1f, 1f, 0.55f);
        public static readonly Color Warning = new Color(1f, 0.8f, 0.3f);

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Panel(string name, Transform parent)
        {
            RectTransform rect = Rect(name, parent);
            Stretch(rect);
            return rect;
        }

        public static TMP_Text Label(string name, Transform parent, string key, float size, FontStyles style, Color color)
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

        public static Button Button(string name, Transform parent, string key, Vector2 size, out TMP_Text label, float fontSize = 38f)
        {
            RectTransform rect = Rect(name, parent);
            rect.sizeDelta = size;
            var image = rect.gameObject.AddComponent<Image>();
            image.color = PanelColor;
            var button = rect.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.pressedColor = new Color(0.8f, 0.3f, 0.25f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            button.colors = colors;

            label = Label("Label", rect, key, fontSize, FontStyles.Bold, Color.white);
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(28f, 0f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            return button;
        }

        public static TMP_InputField InputField(string name, Transform parent, string placeholderKey, Vector2 size)
        {
            GameObject go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
            go.name = name;
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(0.15f, 0.16f, 0.19f, 1f);

            var field = go.GetComponent<TMP_InputField>();
            field.keyboardType = TouchScreenKeyboardType.NumbersAndPunctuation;
            field.characterLimit = 21;
            field.pointSize = 36f;
            var text = (TextMeshProUGUI)field.textComponent;
            text.color = Color.white;
            text.fontSize = 36f;

            var placeholder = (TextMeshProUGUI)field.placeholder;
            placeholder.fontSize = 36f;
            placeholder.color = Muted;
            var localized = placeholder.gameObject.AddComponent<LocalizedText>();
            var so = new SerializedObject(localized);
            so.FindProperty("_key").stringValue = placeholderKey;
            so.ApplyModifiedPropertiesWithoutUndo();
            return field;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        public static void Assign(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void AssignArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty array = so.FindProperty(field);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
