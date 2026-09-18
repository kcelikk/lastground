using LastGround.Core.Events;
using LastGround.Core.Random;
using LastGround.Core.Tick;
using LastGround.Data.Map;
using LastGround.Data.Objectives;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Director;
using LastGround.Gameplay.Loot;
using LastGround.Gameplay.Players;
using Unity.Mathematics;

namespace LastGround.Gameplay.Objectives
{
    /// <summary>
    /// Host: "Clear the area" objectives (TDD_01 §12.4, D-019). After a delay a zone is chosen (never the last one);
    /// team kills inside it count towards a target fixed at the start (players and threat scale it). Completing it
    /// grants a breather (the director relaxes) and a reward pile in the zone, shows "completed" for a moment, then a
    /// cooldown before the next zone. The director keeps spawning throughout — the counter is not a wave.
    /// </summary>
    public sealed class ObjectiveSystem : ITickable
    {
        readonly CrowdState _crowd;
        readonly PlayerStateTable _players;
        readonly MapZoneSet _zones;
        readonly ObjectiveDefinition _definition;
        readonly RunStatus _status;
        readonly ObjectiveState _state;
        DeterministicRandom _rng;
        EventReader<CrowdDeath> _deaths;
        float _timer;
        ushort _instance;
        int _lastZone = -1;
        int _current;
        int _target;

        public ObjectiveSystem(CrowdState crowd, PlayerStateTable players, MapZoneSet zones, ObjectiveDefinition definition,
            RunStatus status, ObjectiveState state, uint seed)
        {
            _crowd = crowd;
            _players = players;
            _zones = zones;
            _definition = definition;
            _status = status;
            _state = state;
            _rng = DeterministicRandom.ForStream(seed, "objectives");
            _deaths = crowd.Deaths.CreateReader();
            _timer = definition.FirstDelay;
        }

        /// <summary>Completion gives the team a breather.</summary>
        public HordeDirector Director { get; set; }

        /// <summary>Completion drops the reward here.</summary>
        public PickupRegistry Loot { get; set; }

        public int Completed { get; private set; }

        public void Tick(float dt, uint tick)
        {
            bool counting = _state.Phase == ObjectivePhase.Active;
            MapZoneSet.Zone zone = counting ? _zones.Zones[_state.Zone] : default;
            while (_crowd.Deaths.TryRead(ref _deaths, out CrowdDeath death))
                if (counting && zone.Contains(death.X, death.Z)) _current++;

            switch (_state.Phase)
            {
                case ObjectivePhase.None:
                    _timer -= dt;
                    if (_timer <= 0f && _zones != null && _zones.Zones.Length > 0) Begin();
                    break;
                case ObjectivePhase.Active:
                    if (_current >= _target) Complete(zone);
                    else _state.Set(_instance, _state.Zone, _current, _target, ObjectivePhase.Active);
                    break;
                case ObjectivePhase.Completed:
                    _timer -= dt;
                    if (_timer > 0f) break;
                    _timer = _definition.Cooldown;
                    _state.Set(_instance, _state.Zone, _current, _target, ObjectivePhase.None);
                    break;
            }
        }

        void Begin()
        {
            int count = _zones.Zones.Length;
            int pick = _rng.Range(0, count);
            if (pick == _lastZone && count > 1) pick = (pick + 1 + _rng.Range(0, count - 1)) % count;
            _lastZone = pick;
            _instance++;
            _current = 0;
            int players = 0;
            for (int p = 0; p < PlayerStateTable.Max; p++) if (_players.Active[p]) players++;
            _target = math.max(1, (int)math.round(_definition.TargetBase * (1f + math.max(0, players - 1) * _definition.TargetPerExtraPlayer))
                                  + (_status.Threat - 1) * _definition.TargetPerThreat);
            _state.Set(_instance, pick, 0, _target, ObjectivePhase.Active);
        }

        void Complete(in MapZoneSet.Zone zone)
        {
            Completed++;
            _current = _target;
            _state.Set(_instance, _state.Zone, _current, _target, ObjectivePhase.Completed);
            _timer = _definition.CompletedShowTime;
            Director?.ForceRelax();
            int players = 0;
            for (int p = 0; p < PlayerStateTable.Max; p++) if (_players.Active[p]) players++;
            Loot?.SpawnReward(new float2(zone.Center.x, zone.Center.y), _definition.RewardCoinsPerPlayer * math.max(1, players),
                _definition.RewardMedkits);
        }
    }
}
