using LastGround.Core.Tick;
using LastGround.Data.Combat;
using LastGround.Gameplay.Players;
using LastGround.Gameplay.Projectiles;
using Unity.Mathematics;

namespace LastGround.Gameplay.Combat
{
    /// <summary>
    /// Host loadouts (TDD_01 §3.5, §6.6): joining players get the starting primary, sidearm and grenades; grenade throws
    /// are checked (standing, has one, cooldown, range) and launched as projectiles; weapon and grenade pickups change
    /// the loadout. Throws are queued and handled in the Combat phase.
    /// </summary>
    public sealed class LoadoutAuthority : ITickable, IGrenadeSink
    {
        struct ThrowRequest
        {
            public int Player;
            public float2 Target;
        }

        const int QueueCapacity = 16;

        readonly LoadoutTable _loadouts;
        readonly PlayerStateTable _players;
        readonly CombatCatalog _catalog;
        readonly ProjectileSystem _projectiles;
        readonly float[] _cooldown = new float[PlayerStateTable.Max];
        readonly ThrowRequest[] _queue = new ThrowRequest[QueueCapacity];
        int _count;

        public LoadoutAuthority(LoadoutTable loadouts, PlayerStateTable players, CombatCatalog catalog, ProjectileSystem projectiles)
        {
            _loadouts = loadouts;
            _players = players;
            _catalog = catalog;
            _projectiles = projectiles;
        }

        public int Thrown { get; private set; }
        public int Refused { get; private set; }

        public void Throw(int player, float2 target)
        {
            if (_count >= QueueCapacity || (uint)player >= PlayerStateTable.Max)
            {
                Refused++;
                return;
            }
            _queue[_count++] = new ThrowRequest { Player = player, Target = target };
        }

        public void Tick(float dt, uint tick)
        {
            for (int p = 0; p < PlayerStateTable.Max; p++)
            {
                if (_cooldown[p] > 0f) _cooldown[p] -= dt;
                if (!_players.Active[p])
                {
                    if (_loadouts.HasLoadout(p)) _loadouts.Clear(p);
                    continue;
                }
                if (!_loadouts.HasLoadout(p))
                    _loadouts.Set(p, _catalog.StartPrimary.NetIndex, _catalog.StartSidearm.NetIndex, (byte)_catalog.StartGrenades);
            }
            for (int i = 0; i < _count; i++) Resolve(_queue[i]);
            _count = 0;
        }

        void Resolve(in ThrowRequest request)
        {
            int p = request.Player;
            if (!_players.CanAct(p) || _loadouts.Grenades[p] == 0 || _cooldown[p] > 0f || _catalog.Grenade == null)
            {
                Refused++;
                return;
            }
            var origin = new float2(_players.X[p], _players.Z[p]);
            if (_projectiles.Fire(_catalog.Grenade, origin, request.Target, p) < 0)
            {
                Refused++;
                return;
            }
            _cooldown[p] = _catalog.GrenadeCooldown;
            _loadouts.Set(p, _loadouts.Primary[p], _loadouts.Sidearm[p], (byte)(_loadouts.Grenades[p] - 1));
            Thrown++;
        }

        /// <summary>A weapon pickup: it replaces the primary. False for unknown weapons or sidearms.</summary>
        public bool GiveWeapon(int player, int netIndex)
        {
            var weapon = _catalog.Weapon(netIndex);
            if (weapon == null || weapon.Slot != Data.Weapons.WeaponSlot.Primary || !_players.CanAct(player)) return false;
            _loadouts.Set(player, weapon.NetIndex, _loadouts.Sidearm[player], _loadouts.Grenades[player]);
            return true;
        }

        /// <summary>A grenade pickup. False when the player already carries the maximum.</summary>
        public bool AddGrenade(int player)
        {
            if (!_players.CanAct(player) || _loadouts.Grenades[player] >= _catalog.MaxGrenades) return false;
            _loadouts.Set(player, _loadouts.Primary[player], _loadouts.Sidearm[player], (byte)(_loadouts.Grenades[player] + 1));
            return true;
        }
    }
}
