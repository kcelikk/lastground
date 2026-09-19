using LastGround.Core.Events;
using LastGround.Core.Tick;
using LastGround.Data.Weapons;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Navigation;
using LastGround.Gameplay.Players;
using Unity.Mathematics;

namespace LastGround.Gameplay.Combat
{
    /// <summary>
    /// Other players' shots, for presentation only (TDD_02 §16: "fire counter + HitFx"): while a remote player's
    /// replicated Firing flag is set, publishes a ShotFired at the rate of the weapon in their hand (loadout + active
    /// slot) along their facing, ending at the first zombie or wall this device sees; shotguns spray their pellets.
    /// No claims, no damage. Their hits arrive through the snapshot hit flag.
    /// </summary>
    public sealed class RemoteShotEmitter : ITickable
    {
        const float MuzzleForward = 0.6f;
        const int MaxHits = 4;

        readonly PlayerStateTable _players;
        readonly ICrowdRenderSource _crowd;
        readonly NavGrid _nav;
        readonly LoadoutTable _loadouts;
        readonly EventChannel<ShotFired> _shots;
        ushort _visualSeq;
        readonly float[] _timer = new float[PlayerStateTable.Max];
        readonly int[] _slots = new int[MaxHits];
        readonly float[] _distances = new float[MaxHits];

        public RemoteShotEmitter(PlayerStateTable players, ICrowdRenderSource crowd, NavGrid nav, LoadoutTable loadouts,
            EventChannel<ShotFired> shots)
        {
            _players = players;
            _crowd = crowd;
            _nav = nav;
            _loadouts = loadouts;
            _shots = shots;
        }

        public void Tick(float dt, uint tick)
        {
            for (int p = 0; p < PlayerStateTable.Max; p++)
            {
                if (p == _players.Local.Value || !_players.Active[p] || !_players.Firing[p] || !_players.CanAct(p))
                {
                    _timer[p] = 0f;
                    continue;
                }
                WeaponDefinition weapon = _loadouts.InSlot(p, _players.ActiveSlot[p]) ?? _loadouts.InSlot(p, 0);
                if (weapon == null) continue;
                _timer[p] -= dt;
                if (_timer[p] > 0f) continue;
                _timer[p] += weapon.ShotInterval;
                if (_timer[p] < 0f) _timer[p] = 0f;
                Emit(p, weapon);
            }
        }

        void Emit(int p, WeaponDefinition weapon)
        {
            _players.GetDisplay(p, out float x, out float z, out float yaw);
            math.sincos(math.radians(yaw), out float sin, out float cos);
            var baseDir = new float2(sin, cos);
            var origin = new float2(x, z);
            int maxHits = math.min(MaxHits, weapon.Penetration + 1);
            int pellets = math.clamp(weapon.PelletCount, 1, 8);
            uint seed = ShotRng.Seed(0x7E3u, p, ++_visualSeq);
            for (int pellet = 0; pellet < pellets; pellet++)
            {
                float angle = ShotRng.SpreadRadians(seed, pellet, weapon.SpreadDeg);
                math.sincos(angle, out float s, out float c);
                var dir = new float2(baseDir.x * c - baseDir.y * s, baseDir.x * s + baseDir.y * c);
                HitQuery.Cast(_crowd, _nav, origin, dir, weapon.Range, WeaponController.HitRadius, maxHits, _slots, _distances, out float end);
                float2 muzzle = origin + baseDir * MuzzleForward;
                float2 tip = origin + dir * math.max(end, MuzzleForward);
                _shots.Publish(new ShotFired
                {
                    Shooter = (byte)p, Weapon = weapon.NetIndex, OriginX = muzzle.x, OriginZ = muzzle.y, EndX = tip.x, EndZ = tip.y,
                    FirstPellet = pellet == 0,
                });
            }
        }
    }
}
