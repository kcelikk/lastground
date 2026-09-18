using TMPro;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UI;

namespace LastGround.UI.Diagnostics
{
    /// <summary>
    /// Development overlay: FPS, frame time, GC allocation per frame, memory, quality level (TDD_02 §22.6).
    /// Created by the app root in editor and development builds only. Text is diagnostic and not localized.
    /// Updates at 2 Hz with allocation-free TMP SetText.
    /// </summary>
    public sealed class PerfHud : MonoBehaviour
    {
        const float RefreshInterval = 0.5f;

        TMP_Text _line1;
        TMP_Text _line2;
        ProfilerRecorder _gcAlloc;
        ProfilerRecorder _usedMemory;

        float _windowTime;
        int _windowFrames;
        float _windowMaxDt;
        long _windowMaxGc;

        public static PerfHud Create()
        {
            var root = new GameObject("[PerfHud]");
            DontDestroyOnLoad(root);

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;

            var hud = root.AddComponent<PerfHud>();
            hud._line1 = CreateLine(root.transform, "Line1", -8f);
            hud._line2 = CreateLine(root.transform, "Line2", -34f);
            return hud;
        }

        static TMP_Text CreateLine(Transform parent, string name, float y)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(12f, y);
            rect.sizeDelta = new Vector2(900f, 30f);

            var text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = 22f;
            text.color = new Color(0.55f, 1f, 0.55f, 0.9f);
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        void OnEnable()
        {
            _gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            _usedMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
        }

        void OnDisable()
        {
            _gcAlloc.Dispose();
            _usedMemory.Dispose();
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _windowTime += dt;
            _windowFrames++;
            if (dt > _windowMaxDt) _windowMaxDt = dt;
            if (_gcAlloc.Valid && _gcAlloc.LastValue > _windowMaxGc) _windowMaxGc = _gcAlloc.LastValue;

            if (_windowTime < RefreshInterval) return;

            float avgMs = _windowTime / _windowFrames * 1000f;
            _line1.SetText("FPS {0:0}  {1:1} ms  max {2:1} ms", _windowFrames / _windowTime, avgMs, _windowMaxDt * 1000f);

            float usedMb = _usedMemory.Valid ? _usedMemory.LastValue / (1024f * 1024f) : 0f;
            _line2.SetText("GC {0:0} B/f max  MEM {1:0} MB  Q{2:0}", _windowMaxGc, usedMb, QualitySettings.GetQualityLevel());

            _windowTime = 0f;
            _windowFrames = 0;
            _windowMaxDt = 0f;
            _windowMaxGc = 0;
        }
    }
}
