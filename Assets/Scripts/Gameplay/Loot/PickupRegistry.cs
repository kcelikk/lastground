using LastGround.Core.Events;
using LastGround.Core.Random;
using LastGround.Core.Tick;
using LastGround.Data.Loot;
using LastGround.Data.Upgrades;
using LastGround.Data.Zombies;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Players;
using LastGround.Gameplay.Upgrades;
using Unity.Mathematics;

namespace LastGround.Gameplay.Loot
{
    /// <summary>
    /// Host loot (TDD_01 §13.2 LootService + PickupRegistry): kills roll coin and medkit drops; coin drops in the same
    /// cell within a short window merge into one pile. Claims are checked (owner bit, distance, alive); a coin pile pays
    /// the team wallet, a medkit heals only the claimer and stays for the others. Pickups expire. Kill ownership never
    /// matters (D-002). Allocation-free.
    /// </summary>
    public sealed class PickupRegistry : ITickable, IPickupClaimSink
    {
        struct Cell
        {
            public int2 Key;
            public int Value;
            public float2 Sum;
            public int Count;
            public float Age;
        }

        const int MaxCells = 64;
        const byte Expired = 255;

        readonly PickupTable _table;
        readonly CrowdState _crowd;
        readonly PlayerStateTable _players;
        readonly LootDefinition _loot;
        readonly ZombieDefinition _walker;
        readonly TeamWallet _wallet;
        readonly float[] _expiry;
        readonly Cell[] _cells = new Cell[MaxCells];
        DeterministicRandom _rng;
        EventReader<CrowdDeath> _deaths;
        int _cellCount;
        int _nextId;

        public PickupRegistry(PickupTable table, CrowdState crowd, PlayerStateTable players, LootDefinition loot,
            ZombieDefinition walker, TeamWallet wallet, uint seed)
        {
            _table = table;
            _crowd = crowd;
            _players = players;
            _loot = loot;
            _walker = walker;
            _wallet = wallet;
            _expiry = new float[table.Capacity];
            _rng = DeterministicRandom.ForStream(seed, "loot");
            _deaths = crowd.Deaths.CreateReader();
        }

        /// <summary>Medkit healing and "full health" checks.</summary>
        public PlayerHealthSystem Health { get; set; }

        /// <summary>Scavenger upgrades widen the pickup radius.</summary>
        public TeamBuilds Builds { get; set; }

        public int Dropped { get; private set; }
        public int Claimed { get; private set; }
        public int Rejected { get; private set; }

        public float RadiusOf(int player)
        {
            float bonus = Builds != null ? Builds.Of(player).Get(StatId.PickupRadiusPct) / 100f : 0f;
            return _loot.PickupRadius * (1f + bonus);
        }

        public void Tick(float dt, uint tick)
        {
            while (_crowd.Deaths.TryRead(ref _deaths, out CrowdDeath death)) OnDeath(death);
            FlushCells(dt);
            for (int id = 0; id < _table.Capacity; id++)
            {
                if (!_table.Active[id]) continue;
                _expiry[id] -= dt;
                if (_expiry[id] <= 0f) _table.Take(id, Expired);
            }
        }

        /// <summary>A player asks for a pickup (local collector on the host, network for clients).</summary>
        public bool Claim(int player, int id)
        {
            if ((uint)id >= (uint)_table.Capacity || (uint)player >= PlayerStateTable.Max || !_table.Active[id]
                || (_table.OwnerMask[id] & (1 << player)) == 0 || !_players.CanAct(player))
            {
                Rejected++;
                return false;
            }
            float reach = RadiusOf(player) + _loot.ClaimTolerance;
            if (math.distancesq(new float2(_players.X[player], _players.Z[player]), new float2(_table.X[id], _table.Z[id])) > reach * reach)
            {
                Rejected++;
                return false;
            }
            switch (_table.Type[id])
            {
                case PickupType.Coin:
                    _wallet.Add(_table.Value[id]);
                    break;
                case PickupType.Medkit:
                    if (Health == null || !Health.Heal(player, _loot.MedkitHeal))
                    {
                        Rejected++;
                        return false;
                    }
                    break;
            }
            Claimed++;
            _table.Take(id, player);
            return true;
        }

        void IPickupClaimSink.Claim(int player, int id) => Claim(player, id);

        void OnDeath(in CrowdDeath death)
        {
            if (_rng.NextFloat() < _walker.CoinChance) AddCoin(new float2(death.X, death.Z), _walker.CoinValue);
            if (_rng.NextFloat() < _loot.MedkitChance) Spawn(PickupType.Medkit, new float2(death.X, death.Z), 1, _loot.MedkitLifetime, AllPlayersMask());
        }

        void AddCoin(float2 position, int value)
        {
            var key = (int2)math.floor(position / _loot.CoinCellSize);
            for (int i = 0; i < _cellCount; i++)
            {
                if (!_cells[i].Key.Equals(key)) continue;
                _cells[i].Value += value;
                _cells[i].Sum += position;
                _cells[i].Count++;
                return;
            }
            if (_cellCount >= MaxCells) FlushOldest();
            _cells[_cellCount++] = new Cell { Key = key, Value = value, Sum = position, Count = 1 };
        }

        void FlushCells(float dt)
        {
            for (int i = _cellCount - 1; i >= 0; i--)
            {
                _cells[i].Age += dt;
                if (_cells[i].Age < _loot.CoinMergeWindow) continue;
                SpawnCell(i);
            }
        }

        void FlushOldest()
        {
            int oldest = 0;
            for (int i = 1; i < _cellCount; i++) if (_cells[i].Age > _cells[oldest].Age) oldest = i;
            SpawnCell(oldest);
        }

        void SpawnCell(int i)
        {
            Cell cell = _cells[i];
            Spawn(PickupType.Coin, cell.Sum / cell.Count, cell.Value, _loot.CoinLifetime, 0xFF);
            _cells[i] = _cells[--_cellCount];
        }

        void Spawn(PickupType type, float2 position, int value, float lifetime, byte mask)
        {
            int id = FreeSlot();
            if (id < 0) return;
            _expiry[id] = lifetime;
            Dropped++;
            _table.Put(new PickupSpawned { Id = id, Type = type, X = position.x, Z = position.y, Value = value, OwnerMask = mask });
        }

        int FreeSlot()
        {
            int capacity = System.Math.Min(_table.Capacity, _loot.MaxPickups);
            for (int n = 0; n < capacity; n++)
            {
                int id = (_nextId + n) % capacity;
                if (_table.Active[id]) continue;
                _nextId = (id + 1) % capacity;
                return id;
            }
            return -1;
        }

        byte AllPlayersMask()
        {
            int mask = 0;
            for (int p = 0; p < PlayerStateTable.Max; p++) if (_players.Active[p]) mask |= 1 << p;
            return (byte)mask;
        }
    }
}
