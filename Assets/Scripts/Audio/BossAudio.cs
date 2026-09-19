using LastGround.Core.Events;
using LastGround.Core.Tick;
using LastGround.Data.Boss;
using LastGround.Gameplay.Boss;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Extraction;
using LastGround.Gameplay.Players;
using UnityEngine;

namespace LastGround.Audio
{
    /// <summary>
    /// Boss and extraction sounds (M8) on every device: the roar on arrival, summon and charge windup, the slam's
    /// impact, a thud when it hits a wall, and the rotor of the incoming helicopter while the landing zone is held
    /// (faster and louder as the hold completes). Driven by the replicated boss and extraction states.
    /// </summary>
    public sealed class BossAudio : ITickable
    {
        readonly SfxPlayer _player;
        readonly PlayerStateTable _players;
        readonly BossState _boss;
        readonly BossDefinition _definition;
        readonly ICrowdRenderSource _crowd;
        readonly ExtractionState _extraction;
        EventReader<BossAttackStarted> _attacks;
        BossPhase _phase;
        bool _stunned;
        float _slamAt = -1f;
        Vector2 _slamPosition;
        float _time;
        float _rotorTimer;

        public BossAudio(SfxPlayer player, PlayerStateTable players, BossState boss, BossDefinition definition, ICrowdRenderSource crowd,
            ExtractionState extraction)
        {
            _player = player;
            _players = players;
            _boss = boss;
            _definition = definition;
            _crowd = crowd;
            _extraction = extraction;
            _attacks = boss.Attacks.CreateReader();
        }

        public void Tick(float dt, uint tick)
        {
            _time += dt;
            Vector2 listener = Listener();
            if (_boss.Phase == BossPhase.Intro && _phase != BossPhase.Intro) _player.Play(SfxId.BossRoar, BossPosition(), listener, 1f);
            _phase = _boss.Phase;
            if (_boss.Stunned && !_stunned) _player.Play(SfxId.BossSlam, BossPosition(), listener, 0.6f, 1.4f);
            _stunned = _boss.Stunned;

            while (_boss.Attacks.TryRead(ref _attacks, out BossAttackStarted a))
            {
                var at = new Vector2(a.OriginX, a.OriginZ);
                if (a.Kind == BossAttackKind.SummonScream || a.Kind == BossAttackKind.Charge)
                    _player.Play(SfxId.BossRoar, at, listener, a.Kind == BossAttackKind.Charge ? 0.7f : 1f, a.Kind == BossAttackKind.Charge ? 1.2f : 1f);
                if (a.Kind == BossAttackKind.GroundSlam && _definition != null && a.Attack < _definition.Attacks.Length)
                {
                    _slamAt = _time + _definition.Attacks[a.Attack].TelegraphSeconds;
                    _slamPosition = at;
                }
            }
            if (_slamAt >= 0f && _time >= _slamAt)
            {
                _slamAt = -1f;
                _player.Play(SfxId.BossSlam, _slamPosition, listener, 1f);
            }

            if (!_extraction.IsOpen || (!_extraction.Holding && _extraction.Hold == 0)) return;
            _rotorTimer -= dt;
            if (_rotorTimer > 0f) return;
            float progress = _extraction.Progress;
            _rotorTimer = Mathf.Lerp(0.3f, 0.11f, progress);
            _player.Play(SfxId.RotorThump, new Vector2(_extraction.X, _extraction.Z), listener, Mathf.Lerp(0.25f, 0.9f, progress));
        }

        Vector2 BossPosition()
        {
            int slot = _boss.Slot;
            return (uint)slot < (uint)_crowd.Capacity ? new Vector2(_crowd.X[slot], _crowd.Z[slot]) : Listener();
        }

        Vector2 Listener()
        {
            int me = _players.Local.IsValid ? _players.Local.Value : 0;
            return new Vector2(_players.X[me], _players.Z[me]);
        }
    }
}
