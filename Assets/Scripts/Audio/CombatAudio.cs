using LastGround.Core.Events;
using LastGround.Core.Net;
using LastGround.Core.Tick;
using LastGround.Gameplay.Combat;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Players;
using UnityEngine;

namespace LastGround.Audio
{
    /// <summary>
    /// Combat sound events → SfxPlayer (Presentation phase). Listens to shots, hits, deaths and player damage; it
    /// does not know whether they came from the local simulation or the network.
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
        uint _variation = 1;

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
                _player.Play(SfxId.Gunshot, new Vector2(shot.OriginX, shot.OriginZ), listener, shot.Local ? 0.55f : 0.4f, Pitch(0.06f));
            }
            while (_hits.TryRead(ref _hitReader, out CrowdHit hit))
                _player.Play(SfxId.ZombieHit, new Vector2(hit.X, hit.Z), listener, 0.45f, Pitch(0.15f));
            while (_deaths.TryRead(ref _deathReader, out CrowdDeath death))
                _player.Play(SfxId.ZombieDeath, new Vector2(death.X, death.Z), listener, 0.6f, Pitch(0.2f));
            int me = _players.Local.IsValid ? _players.Local.Value : -1;
            while (_players.Hurt.TryRead(ref _hurtReader, out PlayerHurt hurt))
                if (hurt.Player == me) _player.Play(SfxId.PlayerHurt, listener, listener, hurt.Died ? 1f : 0.8f, Pitch(0.1f));
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
