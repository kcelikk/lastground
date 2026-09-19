using LastGround.Core.Events;
using LastGround.Core.Services;
using LastGround.Data.Meta;
using LastGround.Gameplay.Players;
using LastGround.Localization;
using TMPro;
using UnityEngine;

namespace LastGround.UI.Run
{
    /// <summary>Speech bubble above a player while an emote plays (M9); one label per player, positioned each frame.</summary>
    public sealed class EmoteBubbles : MonoBehaviour
    {
        const float HeadHeight = 2.4f;

        [SerializeField] TMP_Text[] _labels;

        readonly float[] _until = new float[PlayerStateTable.Max];
        PlayerStateTable _players;
        MetaCatalog _catalog;
        Camera _camera;
        RectTransform _canvas;
        ILocalizationService _localization;
        EventReader<PlayerEmote> _reader;

        public void Bind(PlayerStateTable players, MetaCatalog catalog, Camera camera)
        {
            _players = players;
            _catalog = catalog;
            _camera = camera;
            _canvas = (RectTransform)GetComponentInParent<Canvas>().transform;
            _localization = AppServices.Get<ILocalizationService>();
            _reader = players.Emotes.CreateReader();
            foreach (TMP_Text label in _labels) label.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (_players == null) return;
            while (_players.Emotes.TryRead(ref _reader, out PlayerEmote e))
            {
                EmoteDefinition emote = MetaCatalog.At(_catalog.Emotes, e.Emote);
                if (emote == null || e.Player >= _labels.Length) continue;
                _labels[e.Player].text = _localization.Get(emote.BubbleKey);
                _until[e.Player] = Time.unscaledTime + emote.Seconds;
            }
            int me = _players.Local.IsValid ? _players.Local.Value : -1;
            for (int p = 0; p < _labels.Length; p++)
            {
                bool show = _players.Active[p] && Time.unscaledTime < _until[p];
                if (_labels[p].gameObject.activeSelf != show) _labels[p].gameObject.SetActive(show);
                if (!show) continue;
                float x = _players.X[p], z = _players.Z[p];
                if (p != me) _players.GetDisplay(p, out x, out z, out _);
                Vector3 viewport = _camera.WorldToViewportPoint(new Vector3(x, HeadHeight, z));
                Vector2 size = _canvas.rect.size;
                _labels[p].rectTransform.anchoredPosition = new Vector2((viewport.x - 0.5f) * size.x, (viewport.y - 0.5f) * size.y + 40f);
            }
        }
    }
}
