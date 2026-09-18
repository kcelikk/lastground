using LastGround.Core.Tick;
using LastGround.Data.Loot;
using LastGround.Data.Upgrades;
using LastGround.Gameplay.Players;
using LastGround.Gameplay.Upgrades;

namespace LastGround.Gameplay.Loot
{
    /// <summary>Where the local player's pickup requests go (host: registry; client: network).</summary>
    public interface IPickupClaimSink
    {
        void Claim(int player, int id);
    }

    /// <summary>
    /// The local player walks over loot (TDD_01 §13.2): pickups in radius are requested at once and hidden locally
    /// (instant feel); the host confirms. If no confirmation arrives in time the pickup shows again. Medkits are not
    /// requested at full health, ammo not with full reserves, grenades not with a full belt. Weapons are never taken by
    /// walking over them: the nearest one in reach is offered (<see cref="NearbyWeapon"/>) and taken with a button.
    /// </summary>
    public sealed class PickupCollector : ITickable
    {
        const float ClaimTimeout = 1.5f;

        readonly PickupTable _table;
        readonly PlayerStateTable _players;
        readonly LootDefinition _loot;
        readonly IPickupClaimSink _sink;

        public PickupCollector(PickupTable table, PlayerStateTable players, LootDefinition loot, IPickupClaimSink sink)
        {
            _table = table;
            _players = players;
            _loot = loot;
            _sink = sink;
        }

        public TeamBuilds Builds { get; set; }

        /// <summary>Whether ammo would help (the weapon controller). Null = always.</summary>
        public System.Func<bool> NeedsAmmo { get; set; }

        /// <summary>Grenade count check; null = always take grenades.</summary>
        public Combat.LoadoutTable Loadouts { get; set; }
        public int MaxGrenades { get; set; } = int.MaxValue;

        /// <summary>Weapon pickup in reach, or -1 (HUD "take" button).</summary>
        public int NearbyWeapon { get; private set; } = -1;

        /// <summary>Takes the offered weapon (HUD button).</summary>
        public void TakeWeapon()
        {
            int me = _players.Local.IsValid ? _players.Local.Value : -1;
            int id = NearbyWeapon;
            if (me < 0 || id < 0 || !_table.VisibleTo(id, me)) return;
            _table.PendingClaim[id] = ClaimTimeout;
            NearbyWeapon = -1;
            _sink.Claim(me, id);
        }

        public void Tick(float dt, uint tick)
        {
            for (int id = 0; id < _table.Capacity; id++)
                if (_table.PendingClaim[id] > 0f) _table.PendingClaim[id] -= dt;

            int me = _players.Local.IsValid ? _players.Local.Value : -1;
            NearbyWeapon = -1;
            if (me < 0 || !_players.CanAct(me)) return;
            float bonus = Builds != null ? Builds.Of(me).Get(StatId.PickupRadiusPct) / 100f : 0f;
            float radius = _loot.PickupRadius * (1f + bonus);
            float r2 = radius * radius;
            bool fullHealth = _players.Health[me] >= _players.MaxHealth[me] - 0.01f;
            bool wantsAmmo = NeedsAmmo == null || NeedsAmmo();
            bool wantsGrenade = Loadouts == null || Loadouts.Grenades[me] < MaxGrenades;
            float nearestWeapon = float.MaxValue;
            float x = _players.X[me], z = _players.Z[me];
            for (int id = 0; id < _table.Capacity; id++)
            {
                if (!_table.VisibleTo(id, me)) continue;
                PickupType type = _table.Type[id];
                if ((type == PickupType.Medkit && fullHealth) || (type == PickupType.Ammo && !wantsAmmo)
                    || (type == PickupType.Grenade && !wantsGrenade)) continue;
                float dx = _table.X[id] - x, dz = _table.Z[id] - z;
                float d2 = dx * dx + dz * dz;
                if (d2 > r2) continue;
                if (type == PickupType.Weapon)
                {
                    if (d2 < nearestWeapon)
                    {
                        nearestWeapon = d2;
                        NearbyWeapon = id;
                    }
                    continue;
                }
                _table.PendingClaim[id] = ClaimTimeout;
                _sink.Claim(me, id);
            }
        }
    }
}
