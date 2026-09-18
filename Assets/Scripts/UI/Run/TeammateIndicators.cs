using LastGround.Data.Presentation;
using LastGround.Gameplay.Players;
using UnityEngine;
using UnityEngine.UI;

namespace LastGround.UI.Run
{
    /// <summary>
    /// Screen-edge arrows for teammates outside the view (TDD_01 §14.3), in their slot colour; a downed teammate's
    /// arrow pulses and grows so the team knows who needs a revive and where. Three pooled arrows, no allocation.
    /// </summary>
    public sealed class TeammateIndicators : MonoBehaviour
    {
        const float EdgeInset = 70f;

        [SerializeField] RectTransform[] _arrows;
        [SerializeField] Image[] _images;

        PlayerStateTable _players;
        Camera _camera;
        RectTransform _canvas;
        float _time;

        public void Bind(PlayerStateTable players, Camera camera)
        {
            _players = players;
            _camera = camera;
            _canvas = (RectTransform)transform;
            for (int i = 0; i < _arrows.Length; i++) _arrows[i].gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (_players == null) return;
            _time += Time.unscaledDeltaTime;
            int arrow = 0;
            Vector2 size = _canvas.rect.size;
            for (int p = 0; p < PlayerStateTable.Max && arrow < _arrows.Length; p++)
            {
                if (!_players.Active[p] || p == _players.Local.Value) continue;
                _players.GetDisplay(p, out float x, out float z, out _);
                Vector3 viewport = _camera.WorldToViewportPoint(new Vector3(x, 1f, z));
                bool behind = viewport.z < 0f;
                if (!behind && viewport.x > 0.03f && viewport.x < 0.97f && viewport.y > 0.03f && viewport.y < 0.97f) continue;

                Vector2 fromCentre = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);
                if (behind) fromCentre = -fromCentre;
                Vector2 half = size * 0.5f - new Vector2(EdgeInset, EdgeInset);
                Vector2 scaled = new Vector2(fromCentre.x * size.x, fromCentre.y * size.y);
                float k = Mathf.Min(half.x / Mathf.Max(1e-3f, Mathf.Abs(scaled.x)), half.y / Mathf.Max(1e-3f, Mathf.Abs(scaled.y)));
                RectTransform rect = _arrows[arrow];
                rect.anchoredPosition = scaled * k;
                rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(scaled.y, scaled.x) * Mathf.Rad2Deg - 90f);

                bool downed = _players.Life[p] == PlayerLife.Downed;
                float pulse = downed ? 1f + 0.25f * Mathf.Sin(_time * 10f) : 1f;
                rect.localScale = new Vector3(pulse, pulse, 1f);
                Color color = downed ? Color.Lerp(PlayerSlotColors.Of(p), Color.red, 0.6f) : PlayerSlotColors.Of(p);
                color.a = _players.Life[p] == PlayerLife.Dead ? 0.35f : 0.95f;
                _images[arrow].color = color;
                if (!rect.gameObject.activeSelf) rect.gameObject.SetActive(true);
                arrow++;
            }
            for (; arrow < _arrows.Length; arrow++)
                if (_arrows[arrow].gameObject.activeSelf) _arrows[arrow].gameObject.SetActive(false);
        }
    }
}
