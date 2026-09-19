using LastGround.Core.Services;
using LastGround.Data.Map;
using LastGround.Gameplay.Extraction;
using LastGround.Localization;
using LastGround.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastGround.UI.Run
{
    /// <summary>
    /// Extraction HUD (TDD_01 §9.11 second status line "EXTRACTION 01:12 → GAS STATION"): window time and the landing
    /// zone's region while it is open, hold progress with a bar while someone holds it, "missed" and "extracted"
    /// notes, and a pulsing blue screen-edge arrow towards the zone while it is off screen. Text is rebuilt only when
    /// the state changes, into a reusable character buffer.
    /// </summary>
    public sealed class ExtractionHud : MonoBehaviour
    {
        const float EdgeInset = 90f;

        [SerializeField] GameObject _root;
        [SerializeField] TMP_Text _line;
        [SerializeField] Image _progress;
        [SerializeField] RectTransform _arrow;
        [SerializeField] Image _arrowImage;

        readonly CharLine _text = new CharLine(120);
        ExtractionState _state;
        MapZoneSet _zones;
        Camera _camera;
        RectTransform _canvas;
        ILocalizationService _localization;
        int _shownVersion = -1;
        float _time;

        public void Bind(ExtractionState state, MapZoneSet zones, Camera camera, RectTransform canvas)
        {
            _state = state;
            _zones = zones;
            _camera = camera;
            _canvas = canvas;
            _localization = AppServices.Get<ILocalizationService>();
            _root.SetActive(false);
            _arrow.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (_state == null) return;
            _time += Time.unscaledDeltaTime;
            bool visible = _state.Phase != ExtractionPhase.None;
            if (_root.activeSelf != visible) _root.SetActive(visible);
            if (visible && _state.Version != _shownVersion) Rebuild();
            UpdateArrow();
        }

        void Rebuild()
        {
            _shownVersion = _state.Version;
            _text.Clear();
            switch (_state.Phase)
            {
                case ExtractionPhase.Open:
                    _text.Append(_localization.Get("hud.extraction")).Append("  ");
                    if (_state.Holding || _state.Hold > 0)
                        _text.Append(_state.Hold / 10).Append(" / ").Append(_state.HoldTarget / 10).Append(' ').Append(_localization.Get("hud.seconds_short"));
                    else
                        _text.AppendClock(_state.SecondsLeft);
                    if (_zones != null && (uint)_state.Region < (uint)_zones.Zones.Length)
                        _text.Append("  →  ").Append(_localization.Get(_zones.Zones[_state.Region].NameKey));
                    break;
                case ExtractionPhase.Missed:
                    _text.Append(_localization.Get("hud.extraction_missed"));
                    break;
                default:
                    _text.Append(_localization.Get("hud.extraction_done"));
                    break;
            }
            _line.SetCharArray(_text.Buffer, 0, _text.Length);
            _progress.fillAmount = _state.Phase == ExtractionPhase.Extracted ? 1f : _state.Progress;
            _progress.gameObject.SetActive(_state.Phase != ExtractionPhase.Missed && _state.Hold > 0);
        }

        void UpdateArrow()
        {
            bool show = _state.IsOpen;
            Vector3 viewport = Vector3.zero;
            if (show)
            {
                viewport = _camera.WorldToViewportPoint(new Vector3(_state.X, 0f, _state.Z));
                show = viewport.z < 0f || viewport.x < 0.05f || viewport.x > 0.95f || viewport.y < 0.05f || viewport.y > 0.95f;
            }
            if (_arrow.gameObject.activeSelf != show) _arrow.gameObject.SetActive(show);
            if (!show) return;
            Vector2 size = _canvas.rect.size;
            var fromCentre = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);
            if (viewport.z < 0f) fromCentre = -fromCentre;
            var scaled = new Vector2(fromCentre.x * size.x, fromCentre.y * size.y);
            Vector2 half = size * 0.5f - new Vector2(EdgeInset, EdgeInset);
            float k = Mathf.Min(half.x / Mathf.Max(1e-3f, Mathf.Abs(scaled.x)), half.y / Mathf.Max(1e-3f, Mathf.Abs(scaled.y)));
            _arrow.anchoredPosition = scaled * k;
            _arrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(scaled.y, scaled.x) * Mathf.Rad2Deg - 90f);
            Color color = _arrowImage.color;
            color.a = 0.65f + 0.35f * Mathf.Sin(_time * 5f);
            _arrowImage.color = color;
        }
    }
}
