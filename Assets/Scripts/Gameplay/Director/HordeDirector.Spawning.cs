using LastGround.Gameplay.Players;
using Unity.Mathematics;

namespace LastGround.Gameplay.Director
{
    /// <summary>
    /// Pattern selection, spawn queue and far-zombie cleanup. A pattern's size is in spawn points; each planned spawn
    /// draws its zombie type from the deck (cost, unlock time, weight, cap) and may become an elite.
    /// </summary>
    public sealed partial class HordeDirector
    {
        struct QueuedSpawn
        {
            public float2 Position;
            public byte Type;
            public byte Elite;
        }

        void PlanPattern(int cap)
        {
            int room = cap - _world.Crowd.ActiveCount;
            if (room <= 0) return;
            // Choose once, then save up for it: rerolling every tick would starve the big patterns.
            if (_pendingSize <= 0 || _pendingState != _status.State)
            {
                _pendingPattern = PickPattern();
                _pendingSize = PatternSize(_pendingPattern);
                _pendingState = _status.State;
            }
            HordePattern pattern = _pendingPattern;
            int size = math.min(_pendingSize, room);
            if (size <= 0 || _budget < size) return;
            _pendingSize = 0;

            int player = RandomTargetablePlayer();
            if (player < 0) return;
            if (TryLaunchVirtual(pattern, size, player))
            {
                _budget -= size;
                LastPattern = pattern;
                Patterns++;
                return;
            }
            float angle = _rng.Range(-math.PI, math.PI);
            float spread = math.radians(_profile.PackSpreadDeg);
            int sectors = pattern == HordePattern.Surround ? _rng.Range(_profile.SurroundSectors.x, _profile.SurroundSectors.y + 1) : 1;
            int players = CountTargetable();
            _deck?.CountAlive(_world.Crowd);
            int spent = 0;
            for (int i = 0; spent < size && i < size; i++)
            {
                byte type = 0, elite = 0;
                int cost = 1;
                if (_deck != null)
                {
                    int card = _deck.Pick(_status.RunSeconds, size - spent, math.max(1, players), ref _rng);
                    if (card >= 0)
                    {
                        type = _deck.Card(card).Zombie.TypeIndex;
                        cost = math.max(1, _deck.Card(card).Cost);
                        elite = _deck.RollElite(card, _status.RunSeconds, math.max(1, players), ref _rng);
                    }
                }
                float a;
                float s;
                switch (pattern)
                {
                    case HordePattern.Trickle: a = _rng.Range(-math.PI, math.PI); s = math.PI / 6f; break;
                    case HordePattern.Pack: a = angle; s = spread; break;
                    case HordePattern.Pincer: a = angle + (i % 2) * math.PI; s = spread; break;
                    default: a = angle + (i % sectors) * (2f * math.PI / sectors); s = spread; break;
                }
                if (Plan(player, a, s, type, elite) == 0) continue;
                spent += cost;
            }
            _budget -= spent;
            LastPattern = pattern;
            Patterns++;
        }

        HordePattern _pendingPattern;
        int _pendingSize;
        DirectorState _pendingState;

        public HordePattern LastPattern { get; private set; }
        public int Patterns { get; private set; }

        HordePattern PickPattern()
        {
            // The run's pattern taste shifts rolls towards packs or towards the wider shapes.
            float roll = math.saturate(_rng.NextFloat() + _packBias);
            switch (_status.State)
            {
                case DirectorState.BuildUp:
                    return roll < 0.5f ? HordePattern.Trickle : roll < 0.9f ? HordePattern.Pack : HordePattern.Pincer;
                case DirectorState.Peak:
                    return roll < 0.45f ? HordePattern.Surround : roll < 0.75f ? HordePattern.Pincer : HordePattern.Pack;
                case DirectorState.PeakHold:
                    return roll < 0.2f ? HordePattern.Trickle : roll < 0.7f ? HordePattern.Pack : HordePattern.Pincer;
                default:
                    return HordePattern.Trickle;
            }
        }

        int PatternSize(HordePattern pattern)
        {
            switch (pattern)
            {
                case HordePattern.Trickle: return _rng.Range(_profile.TrickleSize.x, _profile.TrickleSize.y + 1);
                case HordePattern.Pack: return _rng.Range(_profile.PackSize.x, _profile.PackSize.y + 1);
                case HordePattern.Pincer: return 2 * _rng.Range(_profile.PincerSideSize.x, _profile.PincerSideSize.y + 1);
                default:
                    int sectors = _rng.Range(_profile.SurroundSectors.x, _profile.SurroundSectors.y + 1);
                    return sectors * _rng.Range(_profile.SurroundSectorSize.x, _profile.SurroundSectorSize.y + 1);
            }
        }

        int Plan(int player, float angle, float spread, byte type, byte elite)
        {
            if (_queueCount >= _queue.Length) return 0;
            if (!_locator.TryFind(player, angle, spread, ref _rng, out float2 position)) return 0;
            _queue[(_queueHead + _queueCount) % _queue.Length] = new QueuedSpawn { Position = position, Type = type, Elite = elite };
            _queueCount++;
            _deck?.OnQueued(type);
            return 1;
        }

        void SpawnQueued(int cap)
        {
            for (int n = 0; n < _profile.SpawnsPerTick && _queueCount > 0; n++)
            {
                if (_world.Crowd.ActiveCount >= cap)
                {
                    // Over the cap (governor dropped it): give the points back and forget the rest.
                    _budget += _queueCount;
                    while (_queueCount > 0) Dequeue();
                    return;
                }
                QueuedSpawn spawn = Dequeue();
                if (_world.Spawn(spawn.Position, _rng.Range(0f, 360f), spawn.Type, spawn.Elite) < 0) continue;
                Spawned++;
                Announce(spawn);
            }
        }

        QueuedSpawn Dequeue()
        {
            QueuedSpawn spawn = _queue[_queueHead];
            _queueHead = (_queueHead + 1) % _queue.Length;
            _queueCount--;
            _deck?.OnDequeued(spawn.Type);
            return spawn;
        }

        void Announce(in QueuedSpawn spawn)
        {
            if (spawn.Type < _typeSeen.Length && !_typeSeen[spawn.Type])
            {
                _typeSeen[spawn.Type] = true;
                // Walkers are the baseline: no banner for them.
                if (spawn.Type != 0)
                    _status.Announcements.Publish(new DirectorAnnouncement { Kind = AnnouncementKind.NewZombieType, ZombieType = spawn.Type });
            }
            if (spawn.Elite == 0) return;
            Elites++;
            _status.Announcements.Publish(new DirectorAnnouncement
            {
                Kind = AnnouncementKind.EliteSpawned, ZombieType = spawn.Type, Elite = spawn.Elite,
            });
        }

        void DespawnFar()
        {
            float d2 = _profile.DespawnDistance * _profile.DespawnDistance;
            for (int i = 0; i < _world.Crowd.Capacity; i++)
            {
                if (!_world.IsAlive(i) || _world.IsPinned(i)) continue;
                float2 p = _world.PositionOf(i);
                bool near = false;
                for (int k = 0; k < PlayerStateTable.Max && !near; k++)
                    near = _players.Active[k] && math.distancesq(p, new float2(_players.X[k], _players.Z[k])) < d2;
                if (near) continue;
                byte type = _world.TypeOf(i);
                _world.Remove(i, false);
                Despawned++;
                Fold(p, type);
            }
        }

        int RandomTargetablePlayer()
        {
            int count = 0;
            for (int p = 0; p < PlayerStateTable.Max; p++) if (_players.IsTargetable(p)) count++;
            if (count == 0) return -1;
            int pick = _rng.Range(0, count);
            for (int p = 0; p < PlayerStateTable.Max; p++)
            {
                if (!_players.IsTargetable(p)) continue;
                if (pick-- == 0) return p;
            }
            return -1;
        }
    }
}
