using LastGround.Core.Events;
using LastGround.Core.Input;
using LastGround.Data.Combat;
using LastGround.Data.Weapons;
using LastGround.Gameplay.Loot;
using LastGround.Gameplay.Upgrades;
using Unity.Mathematics;

namespace LastGround.Gameplay.Combat
{
    /// <summary>
    /// Loadout side of the weapon controller: follows the loadout table (a new weapon arrives with a full magazine and
    /// full reserve), keeps magazine and reserve per slot, swaps on input (short draw delay) and automatically to the
    /// sidearm when the primary is dry, reloads from reserve, applies the local player's ammo and weapon pickups, and
    /// sends grenade throws.
    /// </summary>
    public sealed partial class WeaponController
    {
        const float SwitchDelay = 0.25f;

        readonly LoadoutTable _loadouts;
        readonly CombatCatalog _catalog;
        readonly WeaponDefinition _standaloneWeapon;
        readonly WeaponDefinition[] _weapon = new WeaponDefinition[2];
        readonly byte[] _known = { LoadoutTable.NoWeapon, LoadoutTable.NoWeapon };
        readonly int[] _ammo = new int[2];
        readonly int[] _reserve = new int[2];
        readonly WeaponStats[] _stats = new WeaponStats[2];
        EventReader<LoadoutChanged> _changedReader;
        EventReader<PickupTaken> _takenReader;
        PickupTable _pickups;
        int _statsVersion = -1;
        int _active;

        /// <summary>Grenade throws go here (host: LoadoutAuthority; client: network). Null = no grenades.</summary>
        public IGrenadeSink Grenades { get; set; }

        /// <summary>Barrels and fuel tanks the local player's bullets can hit (M7). Null = none.</summary>
        public Interactables.InteractableTable Interactables { get; set; }

        /// <summary>Where barrel hits go (host: InteractableSystem; client: network).</summary>
        public Interactables.IInteractableHitSink InteractableHits { get; set; }

        /// <summary>Kept pointed at the weapon in hand (range, assist cone).</summary>
        public AimResolver Aim { get; set; }

        /// <summary>The local player's confirmed ammo and weapon pickups.</summary>
        public PickupTable Pickups
        {
            set
            {
                _pickups = value;
                if (value != null) _takenReader = value.Taken.CreateReader();
            }
        }

        public int Reserve => _reserve[_active];
        public bool InfiniteReserve => _weapon[_active] != null && _weapon[_active].InfiniteReserve;
        public WeaponDefinition Active => _weapon[_active];
        public WeaponDefinition Other => _weapon[1 - _active];
        public int ActiveSlot => _active;

        int IWeaponStatus.Grenades
        {
            get
            {
                int me = _players.Local.IsValid ? _players.Local.Value : -1;
                return me >= 0 ? _loadouts.Grenades[me] : 0;
            }
        }

        /// <summary>The primary could use an ammo pickup (the collector leaves ammo on the ground otherwise).</summary>
        public bool NeedsAmmo
        {
            get
            {
                for (int s = 0; s < 2; s++)
                    if (_weapon[s] != null && !_weapon[s].InfiniteReserve && _reserve[s] < _weapon[s].MaxReserveAmmo) return true;
                return false;
            }
        }

        static LoadoutTable SingleWeapon(WeaponDefinition weapon)
        {
            var weapons = new WeaponDefinition[weapon.NetIndex + 1];
            weapons[weapon.NetIndex] = weapon;
            return new LoadoutTable(weapons);
        }

        void SyncLoadout(int me)
        {
            if (_standaloneWeapon != null && !_loadouts.HasLoadout(me))
                _loadouts.Set(me, _standaloneWeapon.NetIndex, LoadoutTable.NoWeapon, 0);
            while (_loadouts.Changed.TryRead(ref _changedReader, out _)) { }
            for (int s = 0; s < 2; s++)
            {
                byte id = s == 0 ? _loadouts.Primary[me] : _loadouts.Sidearm[me];
                if (id == _known[s]) continue;
                _known[s] = id;
                _weapon[s] = _loadouts.Weapon(id);
                _statsVersion = -1;
                if (_weapon[s] == null) continue;
                _stats[s] = WeaponStats.From(_weapon[s], Builds?.Of(me));
                _ammo[s] = _stats[s].MagazineSize;
                _reserve[s] = _weapon[s].MaxReserveAmmo;
                if (s == _active) SwitchTo(me, s, draw: false);
            }
            if (_weapon[_active] == null && _weapon[1 - _active] != null) SwitchTo(me, 1 - _active, draw: false);
        }

        void RefreshStats(int me)
        {
            PlayerBuild build = Builds?.Of(me);
            int version = build != null ? build.Version : 0;
            if (version == _statsVersion) return;
            _statsVersion = version;
            for (int s = 0; s < 2; s++)
            {
                if (_weapon[s] == null) continue;
                int oldMagazine = _stats[s].MagazineSize;
                _stats[s] = WeaponStats.From(_weapon[s], build);
                // A bigger magazine is usable at once; a full magazine stays full.
                if (_ammo[s] == oldMagazine || _ammo[s] > _stats[s].MagazineSize) _ammo[s] = _stats[s].MagazineSize;
            }
        }

        void ReadPickups(int me)
        {
            if (_pickups == null) return;
            while (_pickups.Taken.TryRead(ref _takenReader, out PickupTaken taken))
            {
                if (taken.Player != me) continue;
                if (taken.Type == PickupType.Ammo) AddAmmo();
                else if (taken.Type == PickupType.Weapon && taken.Value == _loadouts.Primary[me] && _weapon[0] != null) Refill(0);
            }
        }

        /// <summary>An ammo pickup: each limited weapon regains its share of reserve.</summary>
        public void AddAmmo()
        {
            for (int s = 0; s < 2; s++)
            {
                WeaponDefinition weapon = _weapon[s];
                if (weapon == null || weapon.InfiniteReserve) continue;
                int gain = (int)math.ceil(weapon.MaxReserveAmmo * weapon.AmmoPickupFraction);
                _reserve[s] = math.min(weapon.MaxReserveAmmo, _reserve[s] + gain);
            }
        }

        void Refill(int s)
        {
            _ammo[s] = _stats[s].MagazineSize;
            _reserve[s] = _weapon[s].MaxReserveAmmo;
        }

        void HandleSwitchAndGrenade(int me, in PlayerInputFrame frame)
        {
            if (frame.SwitchWeapon && _weapon[1 - _active] != null) SwitchTo(me, 1 - _active);
            if (frame.ThrowGrenade && Grenades != null && _loadouts.Grenades[me] > 0)
                Grenades.Throw(me, new float2(_players.X[me] + frame.GrenadeX, _players.Z[me] + frame.GrenadeY));
        }

        /// <param name="draw">Player-initiated swaps take a short draw time; loadout changes are ready at once.</param>
        void SwitchTo(int me, int slot, bool draw = true)
        {
            _active = slot;
            _players.ActiveSlot[me] = (byte)slot;
            _reloadTimer = 0f;
            if (draw) _cooldown = math.max(_cooldown, SwitchDelay);
            if (Aim != null) Aim.Weapon = _weapon[slot];
        }

        void AutoSwitchWhenDry()
        {
            if (_active != 0 || _ammo[0] > 0 || HasReserve(0) || _weapon[1] == null) return;
            int me = _players.Local.Value;
            SwitchTo(me, 1);
        }

        bool HasReserve(int s) => _weapon[s] != null && (_weapon[s].InfiniteReserve || _reserve[s] > 0);

        void FinishReload(int s)
        {
            if (_weapon[s] == null) return;
            int missing = _stats[s].MagazineSize - _ammo[s];
            if (missing <= 0) return;
            int take = _weapon[s].InfiniteReserve ? missing : math.min(missing, _reserve[s]);
            _ammo[s] += take;
            if (!_weapon[s].InfiniteReserve) _reserve[s] -= take;
        }
    }
}
