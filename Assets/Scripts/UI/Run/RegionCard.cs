using LastGround.Localization;
using LastGround.Core.Services;
using LastGround.Data.Map;
using LastGround.Gameplay.Players;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastGround.UI.Run
{
    /// <summary>
    /// Region card (D-020): when the local player walks into a region, its concept art (docs/reference/maps), name and
    /// danger level fade in under the status line for a few seconds. Checked at 4 Hz; the name is looked up only when
    /// the region changes.
    /// </summary>
    public sealed class RegionCard : MonoBehaviour
    {
        const float CheckInterval = 0.25f;
        const float ShowSeconds = 3.5f;
        const float FadeSeconds = 0.4f;

        [SerializeField] CanvasGroup _group;
        [SerializeField] RawImage _art;
        [SerializeField] TMP_Text _name;
        [SerializeField] TMP_Text _danger;

        MapZoneSet _zones;
        PlayerStateTable _players;
        ILocalizationService _localization;
        string _dangerFormat;
        int _region = -1;
        float _check;
        float _shown = float.MaxValue;

        public void Bind(MapZoneSet zones, PlayerStateTable players)
        {
            _zones = zones;
            _players = players;
            _localization = AppServices.Get<ILocalizationService>();
            _dangerFormat = _localization.Get("region.danger");
            _group.alpha = 0f;
        }

        void Update()
        {
            if (_zones == null || !_players.Local.IsValid) return;
            _shown += Time.unscaledDeltaTime;
            _group.alpha = _shown < FadeSeconds ? _shown / FadeSeconds
                : _shown < ShowSeconds ? 1f
                : Mathf.Max(0f, 1f - (_shown - ShowSeconds) / FadeSeconds);

            _check -= Time.unscaledDeltaTime;
            if (_check > 0f) return;
            _check = CheckInterval;
            int me = _players.Local.Value;
            int region = RegionOf(_players.X[me], _players.Z[me]);
            if (region == _region) return;
            _region = region;
            if (region < 0) return;
            MapZoneSet.Zone zone = _zones.Zones[region];
            _name.text = _localization.Get(zone.NameKey);
            _danger.SetText(_dangerFormat, zone.DangerLevel);
            _art.texture = zone.Concept;
            _art.enabled = zone.Concept != null;
            _shown = 0f;
        }

        int RegionOf(float x, float z)
        {
            for (int i = 0; i < _zones.Zones.Length; i++)
                if (_zones.Zones[i].Contains(x, z)) return i;
            return -1;
        }
    }
}
