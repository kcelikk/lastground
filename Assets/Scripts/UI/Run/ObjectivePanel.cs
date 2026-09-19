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
    /// Objective panel (TDD_01 §9.11, §12.4 HudObjectivePanel, D-019): top-left, title with the region name and a
    /// counter scoped to the event — kills "12/50", seconds left for hold/defend ("Open: 2.4 s", "Defend: 43 s"), the
    /// arrival countdown, or the step to do ("Kill the guard"); flashes "completed"/"failed". Hidden between events.
    /// Only the title is formatted with a string (when an event starts); counters use SetText.
    /// </summary>
    public sealed class ObjectivePanel : MonoBehaviour
    {
        static readonly Color ActiveColor = new Color(1f, 0.85f, 0.35f);
        static readonly Color DoneColor = new Color(0.45f, 1f, 0.5f);
        static readonly Color FailColor = new Color(1f, 0.4f, 0.3f);

        [SerializeField] GameObject _root;
        [SerializeField] TMP_Text _title;
        [SerializeField] TMP_Text _counter;

        ObjectiveState _state;
        MapZoneSet _zones;
        ObjectiveDefinition[] _events;
        ILocalizationService _localization;
        int _shownVersion = -1;
        ushort _titledInstance;
        string _counterFormat, _arriving, _guard, _hunt, _completed, _failed;
        float _flash;

        public void Bind(ObjectiveState state, MapZoneSet zones, ObjectiveDefinition[] events)
        {
            _state = state;
            _zones = zones;
            _events = events;
            _localization = AppServices.Get<ILocalizationService>();
            _arriving = _localization.Get("objective.arriving");
            _guard = _localization.Get("objective.weapon_cache.guard");
            _hunt = _localization.Get("objective.elite_hunt.counter");
            _completed = _localization.Get("objective.completed");
            _failed = _localization.Get("objective.failed");
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
            ObjectiveDefinition definition = DefinitionOf(_state.Kind);
            bool show = _state.Phase != ObjectivePhase.None && definition != null && _zones != null
                        && (uint)_state.Zone < (uint)_zones.Zones.Length;
            if (_root.activeSelf != show) _root.SetActive(show);
            if (!show) return;
            if (_state.Instance != _titledInstance)
            {
                _titledInstance = _state.Instance;
                string zone = _localization.Get(_zones.Zones[_state.Zone].NameKey);
                _title.text = _localization.Format(definition.TitleKey, zone);
                _counterFormat = _localization.Get(definition.CounterKey);
            }
            ShowCounter(definition);
        }

        void ShowCounter(ObjectiveDefinition definition)
        {
            _counter.color = ActiveColor;
            switch (_state.Phase)
            {
                case ObjectivePhase.Completed:
                    _counter.SetText(_completed);
                    _counter.color = DoneColor;
                    _flash = 1f;
                    return;
                case ObjectivePhase.Failed:
                    _counter.SetText(_failed);
                    _counter.color = FailColor;
                    _flash = 1f;
                    return;
                case ObjectivePhase.Announced:
                    _counter.SetText(_arriving, _state.SecondsLeft);
                    return;
            }
            switch (definition.Kind)
            {
                case ObjectiveKind.ClearArea:
                    _counter.SetText(_counterFormat, _state.Current, _state.Target);
                    break;
                case ObjectiveKind.EliteHunt:
                    _counter.SetText(_hunt);
                    break;
                case ObjectiveKind.WeaponCache when _state.Stage == 0:
                    _counter.SetText(_guard);
                    break;
                default:
                    // Hold / defend: seconds still needed.
                    _counter.SetText(_counterFormat, Mathf.Max(0f, (_state.Target - _state.Current) / 10f));
                    break;
            }
        }

        ObjectiveDefinition DefinitionOf(ObjectiveKind kind)
        {
            if (_events == null) return null;
            foreach (ObjectiveDefinition e in _events) if (e != null && e.Kind == kind) return e;
            return null;
        }
    }
}
