using LastGround.Core.Events;
using LastGround.Core.Net;
using LastGround.Core.Tick;
using LastGround.Gameplay.Combat;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Players;
using LastGround.Gameplay.Projectiles;
using UnityEngine;

namespace LastGround.Audio
{
    /// <summary>
    /// Combat sound events → SfxPlayer (Presentation phase). Listens to shots (heavy weapons get a heavier report,
    /// pitch per weapon), hits, deaths, player damage, blasts, spit launches, Exploder fuses lighting up nearby and
    /// the local player's weapon swaps; it does not know whether they came from the local simulation or the network.
    /// </summary>
    public sealed class CombatAudio : ITickable
    {
        readonly SfxPlayer _player;
        readonly PlayerStateTable _players;
        readonly IGameEventStream<ShotFired> _shots;
        readonly IGameEventStream<CrowdHit> _hits;
        readonly IGameEventStream<CrowdDeath> _deaths;
        EventReader<ShotFired> _shotReader;
        EventReader<CrowdHit> _hitReader;
        EventReader<CrowdDeath> _deathReader;
        EventReader<PlayerHurt> _hurtReader;
        EventReader<ExplosionFx> _blastReader;
        EventReader<ProjectileLaunched> _launchReader;
        IGameEventStream<ExplosionFx> _blasts;
        ProjectileTable _projectiles;
        byte[] _primingSeen;
        Data.Weapons.WeaponDefinition _heldWeapon;
        uint _variation = 1;

        /// <summary>Weapons by NetIndex (report and pitch per weapon). Null = one gunshot for all.</summary>
        public Data.Weapons.WeaponDefinition[] Weapons { get; set; }

        /// <summary>Crowd whose Exploder fuses beep when they light up in hearing range. Optional.</summary>
        public ICrowdRenderSource Crowd { get; set; }

        /// <summary>The local player's weapons (swap click). Optional.</summary>
        public IWeaponStatus LocalWeapon { get; set; }

        public IGameEventStream<ExplosionFx> Blasts
        {
            set
            {
                _blasts = value;
                if (value != null) _blastReader = value.CreateReader();
            }
        }

        public ProjectileTable Projectiles
        {
            set
            {
                _projectiles = value;
                if (value != null) _launchReader = value.Launched.CreateReader();
            }
        }

        public CombatAudio(SfxPlayer player, PlayerStateTable players, IGameEventStream<ShotFired> shots,
            IGameEventStream<CrowdHit> hits, IGameEventStream<CrowdDeath> deaths)
        {
            _player = player;
            _players = players;
            _shots = shots;
            _hits = hits;
            _deaths = deaths;
            _shotReader = shots.CreateReader();
            _hitReader = hits.CreateReader();
            _deathReader = deaths.CreateReader();
            _hurtReader = players.Hurt.CreateReader();
        }

        public void Tick(float dt, uint tick)
        {
            Vector2 listener = Listener();
            while (_shots.TryRead(ref _shotReader, out ShotFired shot))
            {
                if (!shot.FirstPellet) continue;
                PlayShot(shot, listener);
            }
            if (_blasts != null)
                while (_blasts.TryRead(ref _blastReader, out ExplosionFx fx))
                    _player.Play(SfxId.Explosion, new Vector2(fx.X, fx.Z), listener, 1f, Pitch(0.15f));
            if (_projectiles != null)
            {
                while (_projectiles.Launched.TryRead(ref _launchReader, out ProjectileLaunched launch))
                    _player.Play(launch.Owner < 0 ? SfxId.Spit : SfxId.Swap, new Vector2(launch.OriginX, launch.OriginZ), listener, 0.6f, Pitch(0.2f));
            }
            BeepNewFuses(listener);
            if (LocalWeapon != null && LocalWeapon.Active != _heldWeapon)
            {
                if (_heldWeapon != null) _player.Play(SfxId.Swap, listener, listener, 0.7f, 1f);
                _heldWeapon = LocalWeapon.Active;
            }
            while (_hits.TryRead(ref _hitReader, out CrowdHit hit))
                _player.Play(SfxId.ZombieHit, new Vector2(hit.X, hit.Z), listener, 0.45f, Pitch(0.15f));
            while (_deaths.TryRead(ref _deathReader, out CrowdDeath death))
                _player.Play(SfxId.ZombieDeath, new Vector2(death.X, death.Z), listener, 0.6f, Pitch(0.2f));
            int me = _players.Local.IsValid ? _players.Local.Value : -1;
            while (_players.Hurt.TryRead(ref _hurtReader, out PlayerHurt hurt))
                if (hurt.Player == me) _player.Play(SfxId.PlayerHurt, listener, listener, hurt.Died ? 1f : 0.8f, Pitch(0.1f));
        }

        void PlayShot(in ShotFired shot, Vector2 listener)
        {
            var weapon = Weapons != null && shot.Weapon < Weapons.Length ? Weapons[shot.Weapon] : null;
            var at = new Vector2(shot.OriginX, shot.OriginZ);
            float volume = shot.Local ? 0.55f : 0.4f;
            if (weapon == null)
            {
                _player.Play(SfxId.Gunshot, at, listener, volume, Pitch(0.06f));
                return;
            }
            bool heavy = (weapon.Tags & (Data.Weapons.WeaponTags.Shotgun | Data.Weapons.WeaponTags.Precision)) != 0;
            // Lighter guns crack higher, heavy ones lower.
            float pitch = Mathf.Clamp(1.25f - weapon.Damage * weapon.PelletCount / 120f, 0.75f, 1.3f);
            _player.Play(heavy ? SfxId.HeavyShot : SfxId.Gunshot, at, listener, heavy ? volume * 1.3f : volume, pitch * Pitch(0.06f));
        }

        void BeepNewFuses(Vector2 listener)
        {
            if (Crowd == null) return;
            if (_primingSeen == null) _primingSeen = new byte[Crowd.Capacity];
            bool[] alive = Crowd.Alive;
            byte[] flags = Crowd.FlagBits;
            for (int i = 0; i < _primingSeen.Length; i++)
            {
                byte priming = alive[i] && (flags[i] & CrowdFlags.Priming) != 0 ? (byte)1 : (byte)0;
                if (priming == 1 && _primingSeen[i] == 0)
                    _player.Play(SfxId.FuseBeep, new Vector2(Crowd.X[i], Crowd.Z[i]), listener, 0.8f, 1f);
                _primingSeen[i] = priming;
            }
        }

        Vector2 Listener()
        {
            if (!_players.Local.IsValid) return Vector2.zero;
            int me = _players.Local.Value;
            return new Vector2(_players.X[me], _players.Z[me]);
        }

        /// <summary>Cheap deterministic pitch variation so repeated sounds do not phase.</summary>
        float Pitch(float range)
        {
            _variation = _variation * 1664525u + 1013904223u;
            return 1f + ((_variation >> 16) / 65535f - 0.5f) * range;
        }
    }
}
