using LastGround.Data.Map;
using LastGround.Data.Objectives;
using LastGround.Gameplay.Players;
using Unity.Mathematics;

namespace LastGround.Gameplay.Objectives
{
    /// <summary>
    /// Per-kind rules (TDD_01 §12.2). Proximity events progress while any standing player is within the radius of the
    /// anchor (no button, like revive); Supply Drop and Weapon Cache let progress fade when nobody is there, Generator
    /// and Rescue Signal hold it but pile on pressure while someone holds. Guards and hunted elites are pinned so the
    /// director never recycles them.
    /// </summary>
    public sealed partial class ObjectiveSystem
    {
        const float ProgressFade = 0.5f;
        const float TrackInterval = 0.5f;

        float _hold;
        float _arrive;
        float _track;
        int _guard = -1;
        byte _guardGeneration;

        void StartKind(int zone, float2 anchor)
        {
            _hold = 0f;
            _guard = -1;
            switch (_active.Kind)
            {
                case ObjectiveKind.ClearArea:
                    int players = ActivePlayers();
                    _target = math.max(1, (int)math.round(_active.TargetBase * (1f + (players - 1) * _active.TargetPerExtraPlayer))
                                          + (_status.Threat - 1) * _active.TargetPerThreat);
                    _state.Set(_instance, zone, 0, _target, ObjectivePhase.Active);
                    break;
                case ObjectiveKind.SupplyDrop:
                    _arrive = _active.ArriveSeconds;
                    _target = Tenths(_active.HoldSeconds);
                    _state.Set(_instance, zone, 0, _target, ObjectivePhase.Announced);
                    _state.SetSeconds((int)math.ceil(_arrive));
                    break;
                case ObjectiveKind.WeaponCache:
                    SpawnMarked(anchor, 4f, 8f, _status.RunSeconds >= 420f ? (byte)2 : (byte)0);
                    _state.Set(_instance, zone, 0, 1, ObjectivePhase.Active);
                    break;
                case ObjectiveKind.EliteHunt:
                    MapZoneSet.Zone z = _zones.Zones[zone];
                    SpawnMarked(anchor, 6f, math.min(z.HalfSize.x, z.HalfSize.y) * 0.8f, _status.RunSeconds >= 180f ? (byte)1 : (byte)0);
                    _state.Set(_instance, zone, 0, 1, ObjectivePhase.Active);
                    if (_guard >= 0) _state.MoveAnchor(World.PositionOf(_guard).x, World.PositionOf(_guard).y);
                    break;
                default:
                    _target = Tenths(_active.HoldSeconds);
                    _state.Set(_instance, zone, 0, _target, ObjectivePhase.Active);
                    break;
            }
        }

        void TickActive(float dt)
        {
            if (_active.TimeLimit > 0f)
            {
                float left = _active.TimeLimit - _elapsed;
                if (left <= 0f)
                {
                    Fail();
                    return;
                }
                if (_state.Phase == ObjectivePhase.Active) _state.SetSeconds((int)math.ceil(left));
            }

            switch (_active.Kind)
            {
                case ObjectiveKind.ClearArea:
                    if (_current >= _target) Complete();
                    else _state.Set(_instance, _state.Zone, _current, _target, ObjectivePhase.Active);
                    break;
                case ObjectiveKind.SupplyDrop:
                    if (_state.Phase == ObjectivePhase.Announced)
                    {
                        _arrive -= dt;
                        _state.SetSeconds((int)math.ceil(math.max(0f, _arrive)));
                        if (_arrive <= 0f) _state.Set(_instance, _state.Zone, 0, _target, ObjectivePhase.Active);
                        break;
                    }
                    Hold(dt, fade: true);
                    break;
                case ObjectiveKind.WeaponCache:
                    if (_state.Stage == 0)
                    {
                        if (GuardAlive()) break;
                        Unpin();
                        _state.SetStage(1);
                        _target = Tenths(_active.HoldSeconds);
                        _state.Set(_instance, _state.Zone, 0, _target, ObjectivePhase.Active);
                        break;
                    }
                    Hold(dt, fade: true);
                    break;
                case ObjectiveKind.EliteHunt:
                    if (!GuardAlive())
                    {
                        Unpin();
                        Complete();
                        break;
                    }
                    _track -= dt;
                    if (_track > 0f) break;
                    _track = TrackInterval;
                    float2 at = World.PositionOf(_guard);
                    _state.MoveAnchor(at.x, at.y);
                    break;
                default:
                    Hold(dt, fade: false);
                    break;
            }
        }

        /// <summary>Progress while someone stands in the circle; completion at the hold time.</summary>
        void Hold(float dt, bool fade)
        {
            bool inside = false;
            float r2 = _active.Radius * _active.Radius;
            for (int p = 0; p < PlayerStateTable.Max && !inside; p++)
            {
                if (!_players.CanAct(p)) continue;
                float dx = _players.X[p] - _state.AnchorX, dz = _players.Z[p] - _state.AnchorZ;
                inside = dx * dx + dz * dz <= r2;
            }
            if (inside)
            {
                _hold += dt;
                if (_active.PressureWhileHolding) Director?.HoldPressure();
            }
            else if (fade)
            {
                _hold = math.max(0f, _hold - dt * ProgressFade);
            }
            _current = math.min(_target, (int)(_hold * 10f));
            if (_hold >= _active.HoldSeconds) Complete();
            else _state.Set(_instance, _state.Zone, _current, _target, ObjectivePhase.Active);
        }

        /// <summary>Spawns a pinned elite guard/target on walkable ground in a ring around the anchor.</summary>
        void SpawnMarked(float2 center, float minRadius, float maxRadius, byte type)
        {
            if (World == null) return;
            byte elite = World.Elites != null && World.Elites.Length > 0 ? World.Elites[_rng.Range(0, World.Elites.Length)].NetIndex : (byte)0;
            for (int attempt = 0; attempt < 16; attempt++)
            {
                float angle = _rng.Range(-math.PI, math.PI), distance = _rng.Range(minRadius, maxRadius);
                float2 p = center + new float2(math.cos(angle), math.sin(angle)) * distance;
                if (!World.Nav.IsWalkable(p)) continue;
                int slot = World.Spawn(p, math.degrees(angle), type, elite);
                if (slot < 0) return;
                _guard = slot;
                _guardGeneration = World.Crowd.Generation[slot];
                World.SetPinned(slot, true);
                return;
            }
        }

        bool GuardAlive() => _guard >= 0 && World.IsAlive(_guard) && World.Crowd.Generation[_guard] == _guardGeneration;

        void Unpin()
        {
            if (_guard >= 0 && World != null) World.SetPinned(_guard, false);
            _guard = -1;
        }

        void EndKind() => Unpin();

        static int Tenths(float seconds) => math.max(1, (int)math.round(seconds * 10f));
    }
}
