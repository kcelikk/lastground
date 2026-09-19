using LastGround.Core.Events;
using LastGround.Core.Random;
using LastGround.Core.Tick;
using LastGround.Data.Map;
using LastGround.Data.Objectives;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Director;
using LastGround.Gameplay.Loot;
using LastGround.Gameplay.Players;
using LastGround.Gameplay.Upgrades;
using LastGround.Gameplay.Zombies;
using Unity.Mathematics;

namespace LastGround.Gameplay.Objectives
{
    /// <summary>
    /// Host map events with objectives (TDD_01 §12.1 MapEventDirector, §12.2, §12.4, D-019). One event at a time:
    /// after a cooldown it picks an unlocked event by weight whose anchor exists in a region other than the last one,
    /// runs it (Clear Area, Supply Drop, Weapon Cache, Power Generator, Rescue Signal, Elite Hunt — see <c>.Kinds</c>),
    /// then pays its reward and gives the team a breather. The director keeps spawning throughout; events only add
    /// pressure (holding a generator, defending a signal) and pull the team across the map (anti-camp by design).
    /// </summary>
    public sealed partial class ObjectiveSystem : ITickable
    {
        readonly CrowdState _crowd;
        readonly PlayerStateTable _players;
        readonly MapZoneSet _zones;
        readonly ObjectiveDefinition[] _events;
        readonly RunStatus _status;
        readonly ObjectiveState _state;
        DeterministicRandom _rng;
        EventReader<CrowdDeath> _deaths;
        ObjectiveDefinition _active;
        float _timer;
        float _elapsed;
        ushort _instance;
        int _lastZone = -1;
        ObjectiveKind _lastKind = (ObjectiveKind)255;
        int _current;
        int _target;

        /// <summary>M5: Clear Area only.</summary>
        public ObjectiveSystem(CrowdState crowd, PlayerStateTable players, MapZoneSet zones, ObjectiveDefinition definition,
            RunStatus status, ObjectiveState state, uint seed)
            : this(crowd, players, zones, new[] { definition }, status, state, seed)
        {
        }

        /// <param name="events">Every event the map may run (catalog order is irrelevant; Kind drives behaviour).</param>
        public ObjectiveSystem(CrowdState crowd, PlayerStateTable players, MapZoneSet zones, ObjectiveDefinition[] events,
            RunStatus status, ObjectiveState state, uint seed)
        {
            _crowd = crowd;
            _players = players;
            _zones = zones;
            _events = events;
            _status = status;
            _state = state;
            _rng = DeterministicRandom.ForStream(seed, "objectives");
            _deaths = crowd.Deaths.CreateReader();
            _timer = float.MaxValue;
            foreach (ObjectiveDefinition e in events) _timer = math.min(_timer, e.FirstDelay);
        }

        /// <summary>Completion gives the team a breather; holding events add pressure.</summary>
        public HordeDirector Director { get; set; }

        /// <summary>Rewards drop here.</summary>
        public PickupRegistry Loot { get; set; }

        /// <summary>Event anchors (supply drops, caches, generators, signals). Null = Clear Area only.</summary>
        public MapDefinition Map { get; set; }

        /// <summary>Spawns cache guards and hunted elites.</summary>
        public ZombieWorld World { get; set; }

        /// <summary>Bonus upgrade offers.</summary>
        public TeamProgress Progress { get; set; }

        /// <summary>Rescue Signal brings the dead back.</summary>
        public PlayerHealthSystem Health { get; set; }

        /// <summary>Generator reward.</summary>
        public SentryTurret Turret { get; set; }

        public int Completed { get; private set; }
        public int Failed { get; private set; }

        public void Tick(float dt, uint tick)
        {
            bool counting = _state.Phase == ObjectivePhase.Active && _active != null && _active.Kind == ObjectiveKind.ClearArea;
            MapZoneSet.Zone zone = counting ? _zones.Zones[_state.Zone] : default;
            while (_crowd.Deaths.TryRead(ref _deaths, out CrowdDeath death))
                if (counting && zone.Contains(death.X, death.Z)) _current++;

            switch (_state.Phase)
            {
                case ObjectivePhase.None:
                    _timer -= dt;
                    if (_timer <= 0f) Begin();
                    break;
                case ObjectivePhase.Announced:
                case ObjectivePhase.Active:
                    _elapsed += dt;
                    TickActive(dt);
                    break;
                default:
                    _timer -= dt;
                    if (_timer > 0f) break;
                    _timer = _active != null ? _active.Cooldown : 25f;
                    _state.Set(_instance, _state.Zone, _current, _target, ObjectivePhase.None);
                    _active = null;
                    break;
            }
            if (Turret == null) return;
            Turret.Tick(dt);
            _state.SetTurret(Turret.Position.x, Turret.Position.y, Turret.Active ? (int)math.ceil(Turret.TimeLeft) : 0);
        }

        void Begin()
        {
            ObjectiveDefinition chosen = Pick(out int zone, out float2 anchor);
            if (chosen == null)
            {
                _timer = 5f;
                return;
            }
            _active = chosen;
            _lastZone = zone;
            _lastKind = chosen.Kind;
            _instance++;
            _elapsed = 0f;
            _current = 0;
            _target = 1;
            _state.SetEvent(chosen.Kind, anchor.x, anchor.y, chosen.Kind == ObjectiveKind.ClearArea ? 0f : chosen.Radius);
            _state.SetSeconds(0);
            StartKind(zone, anchor);
        }

        /// <summary>Weighted pick among unlocked events that have a usable anchor outside the last region.</summary>
        ObjectiveDefinition Pick(out int zone, out float2 anchor)
        {
            zone = -1;
            anchor = float2.zero;
            float total = 0f;
            for (int i = 0; i < _events.Length; i++) total += Eligible(_events[i]) ? Weight(_events[i]) : 0f;
            if (total <= 0f) return null;
            float roll = _rng.NextFloat() * total;
            ObjectiveDefinition chosen = null;
            for (int i = 0; i < _events.Length && chosen == null; i++)
            {
                if (!Eligible(_events[i])) continue;
                roll -= Weight(_events[i]);
                if (roll <= 0f) chosen = _events[i];
            }
            if (chosen == null) return null;
            if (chosen.Kind == ObjectiveKind.ClearArea || chosen.Kind == ObjectiveKind.EliteHunt)
            {
                zone = PickZone();
                MapZoneSet.Zone z = _zones.Zones[zone];
                anchor = new float2(z.Center.x, z.Center.y);
                return chosen;
            }
            return PickAnchor(chosen.Anchor, out zone, out anchor) ? chosen : null;
        }

        /// <summary>The same kind twice in a row only when nothing else is possible.</summary>
        float Weight(ObjectiveDefinition e) => e.Kind == _lastKind ? e.Weight * 0.15f : e.Weight;

        bool Eligible(ObjectiveDefinition e)
        {
            if (e == null || e.Weight <= 0f || _status.RunSeconds < e.MinRunSeconds || _zones == null || _zones.Zones.Length == 0) return false;
            switch (e.Kind)
            {
                case ObjectiveKind.ClearArea: return true;
                case ObjectiveKind.EliteHunt: return World != null;
                case ObjectiveKind.WeaponCache: return World != null && HasAnchor(e.Anchor);
                default: return HasAnchor(e.Anchor);
            }
        }

        bool HasAnchor(MapAnchorKind kind)
        {
            if (Map == null || Map.Anchors == null) return false;
            foreach (MapDefinition.Anchor a in Map.Anchors)
                if (a.Kind == kind && (a.Region != _lastZone || _zones.Zones.Length == 1)) return true;
            return false;
        }

        bool PickAnchor(MapAnchorKind kind, out int zone, out float2 position)
        {
            int count = 0;
            foreach (MapDefinition.Anchor a in Map.Anchors) if (a.Kind == kind && a.Region != _lastZone) count++;
            bool avoidLast = count > 0;
            if (!avoidLast) foreach (MapDefinition.Anchor a in Map.Anchors) if (a.Kind == kind) count++;
            int pick = _rng.Range(0, math.max(1, count));
            foreach (MapDefinition.Anchor a in Map.Anchors)
            {
                if (a.Kind != kind || (avoidLast && a.Region == _lastZone)) continue;
                if (pick-- > 0) continue;
                zone = a.Region;
                position = new float2(a.Position.x, a.Position.y);
                return true;
            }
            zone = -1;
            position = float2.zero;
            return false;
        }

        int PickZone()
        {
            int count = _zones.Zones.Length;
            int pick = _rng.Range(0, count);
            if (pick == _lastZone && count > 1) pick = (pick + 1 + _rng.Range(0, count - 1)) % count;
            return pick;
        }

        void Complete()
        {
            Completed++;
            _state.Set(_instance, _state.Zone, _target, _target, ObjectivePhase.Completed);
            _state.SetSeconds(0);
            _timer = _active.CompletedShowTime;
            Director?.ForceRelax();
            Reward(new float2(_state.AnchorX, _state.AnchorZ));
        }

        void Fail()
        {
            Failed++;
            _state.Set(_instance, _state.Zone, _current, _target, ObjectivePhase.Failed);
            _state.SetSeconds(0);
            _timer = _active.CompletedShowTime;
            EndKind();
        }

        void Reward(float2 at)
        {
            int players = ActivePlayers();
            Loot?.SpawnReward(at, _active.RewardCoinsPerPlayer * players, _active.RewardMedkits);
            if (_active.RewardWeapon) Loot?.DropWeaponAt(at + new float2(1.2f, 0f));
            for (int g = 0; g < _active.RewardGrenades; g++) Loot?.DropGrenadeAt(at + new float2(-1.2f, g * 0.6f));
            if (_active.RewardOfferRarity >= 0) Progress?.GrantBonusOffer((Data.Upgrades.UpgradeRarity)_active.RewardOfferRarity);
            if (_active.RewardRespawn) Health?.ReturnTheDead();
            if (_active.TurretSeconds > 0f) Turret?.Activate(at, _active.TurretSeconds);
            EndKind();
        }

        int ActivePlayers()
        {
            int players = 0;
            for (int p = 0; p < PlayerStateTable.Max; p++) if (_players.Active[p]) players++;
            return math.max(1, players);
        }
    }
}
