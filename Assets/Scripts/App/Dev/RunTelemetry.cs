using System.Globalization;
using LastGround.Core.Net;
using LastGround.Core.Net.Protocol;
using LastGround.Core.Net.Session;
using LastGround.Gameplay.Combat;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Director;
using LastGround.Gameplay.Players;
using LastGround.Gameplay.Upgrades;
using LastGround.Gameplay.Zombies;
using UnityEngine;

namespace LastGround.App.Dev
{
    /// <summary>
    /// Development builds: one log line every 5 s with network and frame stats, so soak tests can be read from
    /// logcat / Player.log (TDD_02 §17.9 CSV-style). Allocates a string every 5 s; not present in release builds.
    /// </summary>
    public sealed class RunTelemetry : MonoBehaviour
    {
        const float Interval = 5f;

        ISession _session;
        ICrowdRenderSource _crowd;
        ZombieWorld _world;
        WeaponController _weapon;
        CombatAuthority _authority;
        PlayerHealthSystem _health;
        PlayerStateTable _players;
        RunStatus _status;
        HordeDirector _director;
        TeamXp _xp;
        TeamBuilds _builds;
        LastGround.Gameplay.Loot.TeamWallet _wallet;
        LastGround.Gameplay.Loot.PickupRegistry _registry;
        LastGround.Gameplay.Combat.LoadoutAuthority _loadouts;
        LastGround.Gameplay.Projectiles.ExplosionSystem _explosions;
        LastGround.Gameplay.Projectiles.ProjectileSystem _projectiles;
        readonly int[] _typeCounts = new int[8];
        float _simMsSum;
        float _simMsMax;
        int _simSamples;
        float _timer;
        int _frames;
        float _maxDt;
        float _elapsed;

        public void Bind(ISession session, ICrowdRenderSource crowd, ZombieWorld world = null)
        {
            _session = session;
            _crowd = crowd;
            _world = world;
        }

        /// <summary>Adds combat counters to the log line (authority and health are host-only, may be null).</summary>
        public void BindCombat(PlayerStateTable players, WeaponController weapon, CombatAuthority authority, PlayerHealthSystem health)
        {
            _players = players;
            _weapon = weapon;
            _authority = authority;
            _health = health;
        }

        /// <summary>Adds M6 counters (host): grenades, blasts, spit hits, zombie types alive, type actions, elites.</summary>
        public void BindContent(LastGround.Gameplay.Combat.LoadoutAuthority loadouts, LastGround.Gameplay.Projectiles.ExplosionSystem explosions,
            LastGround.Gameplay.Projectiles.ProjectileSystem projectiles)
        {
            _loadouts = loadouts;
            _explosions = explosions;
            _projectiles = projectiles;
        }

        /// <summary>Adds run status (all devices) and director internals (host) to the log line.</summary>
        public void BindDirector(RunStatus status, HordeDirector director)
        {
            _status = status;
            _director = director;
        }

        public void BindLoot(LastGround.Gameplay.Loot.TeamWallet wallet, LastGround.Gameplay.Loot.PickupRegistry registry)
        {
            _wallet = wallet;
            _registry = registry;
        }

        public void BindProgress(TeamXp xp, TeamBuilds builds)
        {
            _xp = xp;
            _builds = builds;
        }

        void Update()
        {
            if (_session == null) return;
            float dt = Time.unscaledDeltaTime;
            _timer += dt;
            _elapsed += dt;
            _frames++;
            if (dt > _maxDt) _maxDt = dt;
            if (_world != null)
            {
                _simMsSum += _world.LastTickMs;
                if (_world.LastTickMs > _simMsMax) _simMsMax = _world.LastTickMs;
                _simSamples++;
            }
            if (_timer < Interval) return;

            INetStats s = _session.Stats;
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[NetStats] t={0:0} role={1} players={2} rtt={3:0}ms inKBps={4:0.00} outKBps={5:0.00} inMsg={6} outMsg={7} " +
                "snapKBps={8:0.00} crowd={9} fps={10:0.0} maxFrameMs={11:0.0} simMs={12:0.00} simMaxMs={13:0.00} unstuck={14}",
                _elapsed, _session.Role, _session.Players.Count, _session.Clock.Rtt * 1000.0,
                s.InBytesPerSecond / 1024f, s.OutBytesPerSecond / 1024f, s.InMessagesPerSecond, s.OutMessagesPerSecond,
                (s.InBytesFor(NetMsgId.ZombieSnapshot) + s.OutBytesFor(NetMsgId.ZombieSnapshot)) / 1024f,
                _crowd.ActiveCount, _frames / _timer, _maxDt * 1000f,
                _simSamples > 0 ? _simMsSum / _simSamples : 0f, _simMsMax, _world != null ? _world.Unstuck : 0) + CombatStats());
            _simMsSum = 0f;
            _simMsMax = 0f;
            _simSamples = 0;
            _timer = 0f;
            _frames = 0;
            _maxDt = 0f;
        }

        string ContentStats(int me)
        {
            string line = string.Format(CultureInfo.InvariantCulture, " weapon={0} ammo={1}/{2}", _weapon.Active != null ? _weapon.Active.Id : "-",
                _weapon.Ammo, _weapon.InfiniteReserve ? -1 : _weapon.Reserve);
            if (_world == null) return line;
            System.Array.Clear(_typeCounts, 0, _typeCounts.Length);
            int elites = 0;
            for (int i = 0; i < _world.Crowd.Capacity; i++)
            {
                if (!_world.Crowd.AliveSlots[i]) continue;
                _typeCounts[_world.Crowd.Type[i] & 7]++;
                if (_world.Crowd.Elite[i] != 0) elites++;
            }
            line += string.Format(CultureInfo.InvariantCulture, " types={0}/{1}/{2}/{3}/{4} elitesAlive={5} lunges={6} spits={7} detonations={8}",
                _typeCounts[0], _typeCounts[1], _typeCounts[2], _typeCounts[3], _typeCounts[4], elites, _world.Lunges, _world.Spits, _world.Detonations);
            if (_director != null) line += string.Format(CultureInfo.InvariantCulture, " elites={0}", _director.Elites);
            if (_loadouts != null) line += string.Format(CultureInfo.InvariantCulture, " grenades={0} thrown={1}", ((LastGround.Gameplay.Combat.IWeaponStatus)_weapon).Grenades, _loadouts.Thrown);
            if (_explosions != null) line += string.Format(CultureInfo.InvariantCulture, " blasts={0}", _explosions.Exploded);
            if (_projectiles != null) line += string.Format(CultureInfo.InvariantCulture, " spitHits={0}", _projectiles.PlayerHits);
            return line;
        }

        string CombatStats()
        {
            if (_weapon == null) return string.Empty;
            int me = _players.Local.IsValid ? _players.Local.Value : 0;
            string line = string.Format(CultureInfo.InvariantCulture, " shots={0} claims={1} hp={2:0} life={3}",
                _weapon.ShotsFired, _weapon.ClaimsSent, _players.Health[me], _players.Life[me]);
            if (_authority != null)
            {
                line += string.Format(CultureInfo.InvariantCulture, " accepted={0} rejected={1} kills={2} gone={3} farTarget={4} noLos={5} rate={6} deadShooter={7}",
                    _authority.Accepted, _authority.Rejected, _authority.Kills, _authority.CountOf(HitClaimVerdict.TargetGone),
                    _authority.CountOf(HitClaimVerdict.FarFromTarget), _authority.CountOf(HitClaimVerdict.NoLineOfSight),
                    _authority.CountOf(HitClaimVerdict.RateLimited), _authority.CountOf(HitClaimVerdict.ShooterDead));
            }
            if (_health != null) line += string.Format(CultureInfo.InvariantCulture, " downs={0} playerDeaths={1} revives={2}", _health.Downs, _health.Deaths, _health.Revives);
            if (_world != null) line += string.Format(CultureInfo.InvariantCulture, " zAttacks={0} zDodged={1}", _world.AttacksLanded, _world.AttacksDodged);
            line += ContentStats(me);
            if (_xp != null)
                line += string.Format(CultureInfo.InvariantCulture, " level={0} xp={1}/{2} picks={3} tapped={4} autoPicked={5}", _xp.Level,
                    _xp.Xp, _xp.XpToNext, _builds.Of(me).Picks, LastGround.UI.Run.LevelUpPanel.TappedPicks, LastGround.UI.Run.LevelUpPanel.AutoPicks);
            if (_wallet != null) line += string.Format(CultureInfo.InvariantCulture, " coins={0}", _wallet.Coins);
            if (_registry != null)
                line += string.Format(CultureInfo.InvariantCulture, " drops={0} pickups={1} pickupRejects={2}", _registry.Dropped, _registry.Claimed, _registry.Rejected);
            if (_status != null)
            {
                line += string.Format(CultureInfo.InvariantCulture, " run={0:0} horde={1} threat={2}", _status.RunSeconds, _status.Horde, _status.Threat);
                if (_director != null)
                {
                    line += string.Format(CultureInfo.InvariantCulture,
                        " intensity={0:0.00} state={1} alive={2} maxAlive={3} rate={4:0.0} spawned={5} patterns={6} governor={7:0.00} spawnMiss={8}",
                        _status.Intensity, _status.State, _status.Alive, _status.MaxAlive, _status.SpawnRate, _director.Spawned,
                        _director.Patterns, _director.Governor.Multiplier, _director.SpawnFailures);
                }
            }
            return line;
        }
    }
}
