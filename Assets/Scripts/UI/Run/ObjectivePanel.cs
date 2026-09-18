using LastGround.Core.Services;
using LastGround.Data.Map;
using LastGround.Data.Objectives;
using LastGround.Gameplay.Objectives;
using LastGround.Localization;
using TMPro;
using UnityEngine;

namespace LastGround.UI.Run
{
    /// <summary>
    /// Objective panel (TDD_01 §9.11, §12.4 HudObjectivePanel, D-019): top-left, title with the zone name and a
    /// counter scoped to the objective ("Kill zombies: 12/50"); flashes "completed". Hidden between objectives.
    /// Only the title is formatted with a string (when an objective starts); the counter uses SetText.
    /// </summary>
    public sealed class ObjectivePanel : MonoBehaviour
    {
        static readonly Color ActiveColor = new Color(1f, 0.85f, 0.35f);
        static readonly Color DoneColor = new Color(0.45f, 1f, 0.5f);

        [SerializeField] GameObject _root;
        [SerializeField] TMP_Text _title;
        [SerializeField] TMP_Text _counter;

        ObjectiveState _state;
        MapZoneSet _zones;
        ObjectiveDefinition _definition;
        ILocalizationService _localization;
        int _shownVersion = -1;
        ushort _titledInstance;
        string _counterFormat;
        float _flash;

        public void Bind(ObjectiveState state, MapZoneSet zones, ObjectiveDefinition definition)
        {
            _state = state;
            _zones = zones;
            _definition = definition;
            _localization = AppServices.Get<ILocalizationService>();
            _counterFormat = _localization.Get(definition.CounterKey);
            _root.SetActive(false);
        }

        void Update()
        {
            if (_state == null) return;
            if (_flash > 0f)
            {
                _flash = Mathf.Max(0f, _flash - Time.unscaledDeltaTime);
                float s = 1f + 0.15f * Mathf.Sin(_flash * 20f) * _flash;
                _root.transform.localScale = new Vector3(s, s, 1f);
            }
            if (_state.Version == _shownVersion) return;
            _shownVersion = _state.Version;

            bool show = _state.Phase != ObjectivePhase.None && _zones != null && (uint)_state.Zone < (uint)_zones.Zones.Length;
            if (_root.activeSelf != show) _root.SetActive(show);
            if (!show) return;

            if (_state.Instance != _titledInstance)
            {
                _titledInstance = _state.Instance;
                string zone = _localization.Get(_zones.Zones[_state.Zone].NameKey);
                _title.text = _localization.Format(_definition.TitleKey, zone);
            }
            if (_state.Phase == ObjectivePhase.Completed)
            {
                _counter.SetText(_localization.Get("objective.completed"));
                _counter.color = DoneColor;
                _flash = 1f;
            }
            else
            {
                _counter.SetText(_counterFormat, _state.Current, _state.Target);
                _counter.color = ActiveColor;
            }
        }
    }
}
