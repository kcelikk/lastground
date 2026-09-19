using LastGround.Core.Tick;
using LastGround.Data.Map;
using LastGround.Gameplay.Combat;
using LastGround.Gameplay.Players;
using Unity.Mathematics;

namespace LastGround.Gameplay.Interactables
{
    /// <summary>
    /// Ammo crates on the local device (TDD_01 §12.3 "oyuncu başına cooldown'lu dolum", D-002): standing next to one
    /// refills the local player's reserve like an ammo pickup, then that crate rests for this player only. Ammo is kept by
    /// the player's own weapon, so no host round-trip is needed.
    /// </summary>
    public sealed class LocalStations : ITickable
    {
        readonly InteractableTable _table;
        readonly InteractableProfile _profile;
        readonly PlayerStateTable _players;
        readonly WeaponController _weapon;
        readonly float[] _cooldown;

        public LocalStations(InteractableTable table, InteractableProfile profile, PlayerStateTable players, WeaponController weapon)
        {
            _table = table;
            _profile = profile;
            _players = players;
            _weapon = weapon;
            _cooldown = new float[table.Count];
        }

        public int Refills { get; private set; }

        /// <summary>Seconds until this crate serves the local player again (HUD/world marker).</summary>
        public float CooldownOf(int id) => _cooldown[id];

        public void Tick(float dt, uint tick)
        {
            int me = _players.Local.IsValid ? _players.Local.Value : -1;
            float r2 = _profile.AmmoRadius * _profile.AmmoRadius;
            for (int i = 0; i < _table.Count; i++)
            {
                if (_table.Kind[i] != InteractableKind.AmmoCrate) continue;
                if (_cooldown[i] > 0f)
                {
                    _cooldown[i] -= dt;
                    continue;
                }
                if (me < 0 || !_players.CanAct(me) || !_weapon.NeedsAmmo) continue;
                if (math.distancesq(new float2(_players.X[me], _players.Z[me]), _table.Position[i]) > r2) continue;
                _weapon.AddAmmo();
                _cooldown[i] = _profile.AmmoCooldown;
                Refills++;
            }
        }
    }
}
