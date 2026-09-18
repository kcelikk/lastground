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
    /// replicated Firing flag is set, publishes a ShotFired at the weapon's rate along their facing, ending at the
    /// first zombie or wall this device sees. No claims, no damage. Their hits arrive through the snapshot hit flag.
    /// </summary>
    public sealed class RemoteShotEmitter : ITickable
    {
        const float MuzzleForward = 0.6f;
        const int MaxHits = 4;

        readonly PlayerStateTable _players;
        readonly ICrowdRenderSource _crowd;
        readonly NavGrid _nav;
        readonly WeaponDefinition _weapon;
        readonly EventChannel<ShotFired> _shots;
        readonly float[] _timer = new float[PlayerStateTable.Max];
        readonly int[] _slots = new int[MaxHits];
        readonly float[] _distances = new float[MaxHits];

        public RemoteShotEmitter(PlayerStateTable players, ICrowdRenderSource crowd, NavGrid nav, WeaponDefinition weapon,
            EventChannel<ShotFired> shots)
        {
            _players = players;
            _crowd = crowd;
            _nav = nav;
            _weapon = weapon;
            _shots = shots;
        }

        public void Tick(float dt, uint tick)
        {
            for (int p = 0; p < PlayerStateTable.Max; p++)
            {
                if (p == _players.Local.Value || !_players.Active[p] || !_players.Firing[p] || _players.Dead[p])
                {
                    _timer[p] = 0f;
                    continue;
                }
                _timer[p] -= dt;
                if (_timer[p] > 0f) continue;
                _timer[p] += _weapon.ShotInterval;
                if (_timer[p] < 0f) _timer[p] = 0f;

                _players.GetDisplay(p, out float x, out float z, out float yaw);
                math.sincos(math.radians(yaw), out float sin, out float cos);
                var dir = new float2(sin, cos);
                var origin = new float2(x, z);
                int maxHits = math.min(MaxHits, _weapon.Penetration + 1);
                HitQuery.Cast(_crowd, _nav, origin, dir, _weapon.Range, WeaponController.HitRadius, maxHits, _slots, _distances, out float end);
                float2 muzzle = origin + dir * MuzzleForward;
                float2 tip = origin + dir * math.max(end, MuzzleForward);
                _shots.Publish(new ShotFired
                {
                    Shooter = (byte)p, OriginX = muzzle.x, OriginZ = muzzle.y, EndX = tip.x, EndZ = tip.y, FirstPellet = true,
                });
            }
        }
    }
}
