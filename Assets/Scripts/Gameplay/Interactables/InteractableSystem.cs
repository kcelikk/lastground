using LastGround.Core.Tick;
using LastGround.Data.Combat;
using LastGround.Data.Map;
using LastGround.Gameplay.Navigation;
using LastGround.Gameplay.Players;
using LastGround.Gameplay.Projectiles;
using LastGround.Gameplay.Zombies;
using Unity.Mathematics;

namespace LastGround.Gameplay.Interactables
{
    /// <summary>
    /// Host interactables (TDD_01 §12.3): barrels and fuel tanks lose health to bullets (claims checked for range and
    /// line of sight) and blasts, explode through the <see cref="IExplosionSink"/> (chains follow through the blast
    /// query) and come back after a while; medical stations heal standing players nearby from a recharging pool.
    /// Ammo crates are per-player and handled on each device (<see cref="LocalStations"/>).
    /// </summary>
    public sealed class InteractableSystem : ITickable, IInteractableHitSink, IBlastListener
    {
        const float ClaimRange = 50f;

        readonly InteractableTable _table;
        readonly InteractableProfile _profile;
        readonly PlayerStateTable _players;
        readonly NavGrid _nav;
        readonly IExplosionSink _explosions;
        readonly float[] _health;
        readonly float[] _respawn;
        readonly float[] _charge;

        public InteractableSystem(InteractableTable table, InteractableProfile profile, PlayerStateTable players, NavGrid nav,
            IExplosionSink explosions)
        {
            _table = table;
            _profile = profile;
            _players = players;
            _nav = nav;
            _explosions = explosions;
            _health = new float[table.Count];
            _respawn = new float[table.Count];
            _charge = new float[table.Count];
            for (int i = 0; i < table.Count; i++)
            {
                _health[i] = MaxHealth(i);
                _charge[i] = profile.MedCharge;
            }
        }

        public int Explosions { get; private set; }
        public float Healed { get; private set; }

        public void HitInteractable(int shooter, int id, float damage)
        {
            if ((uint)id >= (uint)_table.Count || !_table.IsExplosive(id) || !_table.Intact[id]) return;
            if ((uint)shooter >= PlayerStateTable.Max || !_players.CanAct(shooter)) return;
            var from = new float2(_players.X[shooter], _players.Z[shooter]);
            if (math.distance(from, _table.Position[id]) > ClaimRange) return;
            if (_nav != null && !_nav.HasLineOfSight(from, _table.Position[id])) return;
            Damage(id, damage, shooter);
        }

        /// <summary>A blast went off: barrels and tanks inside take its damage (chain reactions).</summary>
        public void OnBlast(float2 center, float radius, float damage)
        {
            float r2 = radius * radius;
            for (int i = 0; i < _table.Count; i++)
            {
                if (!_table.IsExplosive(i) || !_table.Intact[i]) continue;
                if (math.distancesq(center, _table.Position[i]) <= r2 && math.distancesq(center, _table.Position[i]) > 0.01f)
                    Damage(i, damage, -1);
            }
        }

        public void Tick(float dt, uint tick)
        {
            for (int i = 0; i < _table.Count; i++)
            {
                if (_table.IsExplosive(i))
                {
                    if (_table.Intact[i]) continue;
                    _respawn[i] -= dt;
                    if (_respawn[i] > 0f) continue;
                    _health[i] = MaxHealth(i);
                    _table.SetIntact(i, true);
                }
                else if (_table.Kind[i] == InteractableKind.MedStation)
                {
                    TickStation(i, dt);
                }
            }
        }

        void TickStation(int i, float dt)
        {
            float r2 = _profile.MedRadius * _profile.MedRadius;
            for (int p = 0; p < PlayerStateTable.Max && _charge[i] > 0f; p++)
            {
                if (!_players.CanAct(p) || _players.Health[p] >= _players.MaxHealth[p] - 0.01f) continue;
                if (math.distancesq(new float2(_players.X[p], _players.Z[p]), _table.Position[i]) > r2) continue;
                float heal = math.min(math.min(_profile.MedHealPerSecond * dt, _charge[i]), _players.MaxHealth[p] - _players.Health[p]);
                _players.Health[p] += heal;
                _charge[i] -= heal;
                Healed += heal;
            }
            _charge[i] = math.min(_profile.MedCharge, _charge[i] + _profile.MedRechargePerSecond * dt);
            _table.SetCharge(i, _charge[i] / _profile.MedCharge);
        }

        void Damage(int id, float damage, int source)
        {
            _health[id] -= damage;
            if (_health[id] > 0f) return;
            _table.SetIntact(id, false);
            _respawn[id] = _profile.RespawnSeconds;
            Explosions++;
            ExplosionSpec spec = _table.Kind[id] == InteractableKind.FuelTank ? _profile.TankBlast : _profile.BarrelBlast;
            // Barrel blasts hurt everyone nearby (they are the environment's, not the shooter's).
            _explosions?.Explode(_table.Position[id], spec, ExplosionKind.Barrel, -1);
        }

        float MaxHealth(int i) => _table.Kind[i] == InteractableKind.FuelTank ? _profile.TankHealth : _profile.BarrelHealth;
    }
}
