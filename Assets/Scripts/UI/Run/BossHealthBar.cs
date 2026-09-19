using LastGround.Core.Services;
using LastGround.Data.Boss;
using LastGround.Gameplay.Boss;
using LastGround.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastGround.UI.Run
{
    /// <summary>
    /// Boss bar (TDD_01 §0.9 panel 7: name + HP at the top centre): shown while the boss lives, with marks at the phase
    /// thresholds, colour by phase (dark red → orange when enraged) and a pale flash while it is stunned against a wall.
    /// </summary>
    public sealed class BossHealthBar : MonoBehaviour
    {
        static readonly Color Phase1 = new Color(0.62f, 0.08f, 0.06f);
        static readonly Color Phase2 = new Color(0.8f, 0.2f, 0.06f);
        static readonly Color Enraged = new Color(1f, 0.45f, 0.08f);
        static readonly Color Stunned = new Color(0.85f, 0.9f, 1f);

        [SerializeField] GameObject _root;
        [SerializeField] TMP_Text _name;
        [SerializeField] Image _fill;
        [SerializeField] RectTransform _phase2Mark;
        [SerializeField] RectTransform _enragedMark;

        BossState _state;
        float _shown = 1f;
        float _time;

        public void Bind(BossState state, BossDefinition definition)
        {
            _state = state;
            if (definition != null)
            {
                _name.text = AppServices.Get<ILocalizationService>().Get(definition.NameKey);
                Mark(_phase2Mark, definition.Phase2At);
                Mark(_enragedMark, definition.EnragedAt);
            }
            _root.SetActive(false);
        }

        static void Mark(RectTransform mark, float fraction)
        {
            mark.anchorMin = new Vector2(fraction, 0f);
            mark.anchorMax = new Vector2(fraction, 1f);
            mark.anchoredPosition = Vector2.zero;
        }

        void Update()
        {
            if (_state == null) return;
            bool show = _state.Active;
            if (_root.activeSelf != show) _root.SetActive(show);
            if (!show)
            {
                _shown = 1f;
                return;
            }
            _time += Time.unscaledDeltaTime;
            // The bar slides down to the true value instead of jumping (hits arrive in bursts).
            _shown = Mathf.MoveTowards(_shown, _state.HealthFraction, Time.unscaledDeltaTime * 0.8f);
            _shown = Mathf.Max(_shown, _state.HealthFraction);
            _fill.fillAmount = _shown;
            Color color = _state.Phase == BossPhase.Enraged ? Enraged : _state.Phase == BossPhase.Phase2 ? Phase2 : Phase1;
            if (_state.Stunned) color = Color.Lerp(color, Stunned, 0.5f + 0.5f * Mathf.Sin(_time * 12f));
            _fill.color = color;
        }
    }
}
