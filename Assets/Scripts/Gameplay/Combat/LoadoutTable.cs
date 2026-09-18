using LastGround.Core.Events;
using LastGround.Data.Weapons;
using LastGround.Gameplay.Players;

namespace LastGround.Gameplay.Combat
{
    /// <summary>A player's loadout changed (new weapon picked up, grenade thrown or picked up).</summary>
    public struct LoadoutChanged
    {
        public int Player;
        public bool WeaponChanged;
    }

    /// <summary>
    /// Every device's view of the players' loadouts (TDD_01 §3.5, §6.6): primary and sidearm weapon ids and grenade
    /// count. The host changes it (<see cref="LoadoutAuthority"/>); clients through replication. Ammo in the magazine and
    /// in reserve is kept by each player's own weapon controller. The active slot lives in <see cref="PlayerStateTable"/>.
    /// </summary>
    public sealed class LoadoutTable
    {
        public const byte NoWeapon = 255;

        public readonly byte[] Primary = { NoWeapon, NoWeapon, NoWeapon, NoWeapon };
        public readonly byte[] Sidearm = { NoWeapon, NoWeapon, NoWeapon, NoWeapon };
        public readonly byte[] Grenades = new byte[PlayerStateTable.Max];

        public readonly EventChannel<LoadoutChanged> Changed = new EventChannel<LoadoutChanged>(32);

        readonly WeaponDefinition[] _weapons;

        /// <param name="weapons">Weapons by NetIndex (CombatCatalog.Weapons).</param>
        public LoadoutTable(WeaponDefinition[] weapons)
        {
            _weapons = weapons;
        }

        public WeaponDefinition Weapon(int netIndex) => (uint)netIndex < (uint)_weapons.Length ? _weapons[netIndex] : null;

        public bool HasLoadout(int player) => Primary[player] != NoWeapon || Sidearm[player] != NoWeapon;

        /// <summary>The weapon in <paramref name="slot"/> (0 primary, 1 sidearm), or null.</summary>
        public WeaponDefinition InSlot(int player, int slot) => Weapon(slot == 0 ? Primary[player] : Sidearm[player]);

        /// <summary>Does the player carry this weapon in either slot?</summary>
        public bool Holds(int player, byte weapon) => weapon != NoWeapon && (Primary[player] == weapon || Sidearm[player] == weapon);

        public void Set(int player, byte primary, byte sidearm, byte grenades)
        {
            if ((uint)player >= PlayerStateTable.Max) return;
            bool weapons = Primary[player] != primary || Sidearm[player] != sidearm;
            if (!weapons && Grenades[player] == grenades) return;
            Primary[player] = primary;
            Sidearm[player] = sidearm;
            Grenades[player] = grenades;
            Changed.Publish(new LoadoutChanged { Player = player, WeaponChanged = weapons });
        }

        public void Clear(int player)
        {
            if ((uint)player >= PlayerStateTable.Max) return;
            Primary[player] = NoWeapon;
            Sidearm[player] = NoWeapon;
            Grenades[player] = 0;
        }
    }
}
