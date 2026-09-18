using LastGround.Data.Map;
using LastGround.Gameplay.Objectives;
using UnityEngine;
using UnityEngine.UI;

namespace LastGround.UI.Run
{
    /// <summary>Screen-edge arrow towards the active objective zone while its centre is off screen (TDD_01 §12.4).</summary>
    public sealed class ObjectiveIndicator : MonoBehaviour
    {
        const float EdgeInset = 90f;

        [SerializeField] RectTransform _arrow;
        [SerializeField] Image _image;

        ObjectiveState _state;
        MapZoneSet _zones;
        Camera _camera;
        RectTransform _canvas;
        float _time;

        public void Bind(ObjectiveState state, MapZoneSet zones, Camera camera)
        {
            _state = state;
            _zones = zones;
            _camera = camera;
            _canvas = (RectTransform)transform;
            _arrow.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (_state == null) return;
            _time += Time.unscaledDeltaTime;
            bool show = _state.Phase == ObjectivePhase.Active && _zones != null && (uint)_state.Zone < (uint)_zones.Zones.Length;
            Vector3 viewport = Vector3.zero;
            if (show)
            {
                Vector2 c = _zones.Zones[_state.Zone].Center;
                viewport = _camera.WorldToViewportPoint(new Vector3(c.x, 0f, c.y));
                show = viewport.z < 0f || viewport.x < 0.05f || viewport.x > 0.95f || viewport.y < 0.05f || viewport.y > 0.95f;
            }
            if (_arrow.gameObject.activeSelf != show) _arrow.gameObject.SetActive(show);
            if (!show) return;

            Vector2 size = _canvas.rect.size;
            Vector2 fromCentre = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);
            if (viewport.z < 0f) fromCentre = -fromCentre;
            Vector2 scaled = new Vector2(fromCentre.x * size.x, fromCentre.y * size.y);
            Vector2 half = size * 0.5f - new Vector2(EdgeInset, EdgeInset);
            float k = Mathf.Min(half.x / Mathf.Max(1e-3f, Mathf.Abs(scaled.x)), half.y / Mathf.Max(1e-3f, Mathf.Abs(scaled.y)));
            _arrow.anchoredPosition = scaled * k;
            _arrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(scaled.y, scaled.x) * Mathf.Rad2Deg - 90f);
            Color color = _image.color;
            color.a = 0.65f + 0.35f * Mathf.Sin(_time * 4f);
            _image.color = color;
        }
    }
}
