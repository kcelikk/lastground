using Unity.Mathematics;

namespace LastGround.Gameplay.Players
{
    /// <summary>Downed bleedout, revive progress, solo adrenaline, and returning from dead.</summary>
    public sealed partial class PlayerHealthSystem
    {
        void TickDowned(int p, float dt, int activePlayers)
        {
            float rate = 0f;
            bool solo = activePlayers == 1;
            if (solo && _adrenaline > 0)
            {
                rate = 1f / _definition.ReviveTime; // Adrenaline: gets back up on their own.
            }
            else
            {
                int reviver = NearestReviver(p);
                if (reviver >= 0)
                    rate = 1f / _definition.ReviveTime * (_sinceHurt[reviver] < RecentHurtWindow ? _definition.HurtReviveFactor : 1f);
            }

            if (rate > 0f)
            {
                _players.ReviveProgress[p] += rate * dt;
                if (_players.ReviveProgress[p] >= 1f)
                {
                    if (solo && _adrenaline > 0) _adrenaline--;
                    Revive(p);
                    return;
                }
            }
            else
            {
                _players.ReviveProgress[p] = math.max(0f, _players.ReviveProgress[p] - _definition.ReviveDecayPerSecond * dt);
            }

            // Nobody can save a solo player without adrenaline: bleed out straight away.
            _players.Countdown[p] -= solo && _adrenaline == 0 ? float.MaxValue : dt;
            if (_players.Countdown[p] > 0f) return;
            _players.Life[p] = PlayerLife.Dead;
            _players.ReviveProgress[p] = 0f;
            _players.Countdown[p] = _definition.RespawnDelay;
            Deaths++;
        }

        void TickDead(int p, float dt, int standing)
        {
            // The timer only runs while someone is still up to come back to.
            if (standing == 0) return;
            _players.Countdown[p] -= dt;
            if (_players.Countdown[p] > 0f) return;
            _players.Life[p] = PlayerLife.Alive;
            _players.Health[p] = _players.MaxHealth[p] * _definition.RespawnHealthFraction;
            _players.Countdown[p] = 0f;
            _downsThisLife[p] = 0;
            MakeInvulnerable(p, _definition.RespawnInvulnerability);
            Respawns.Publish(new PlayerRespawn
            {
                Player = p, X = _players.X[p], Z = _players.Z[p],
                PushRadius = _definition.RespawnPushRadius, PushSpeed = _definition.RespawnPushSpeed,
            });
        }

        void Revive(int p)
        {
            _players.Life[p] = PlayerLife.Alive;
            _players.Health[p] = _players.MaxHealth[p] * _definition.ReviveHealthFraction;
            _players.ReviveProgress[p] = 0f;
            _players.Countdown[p] = 0f;
            MakeInvulnerable(p, _definition.ReviveInvulnerability);
            Revives++;
            Respawns.Publish(new PlayerRespawn
            {
                Player = p, X = _players.X[p], Z = _players.Z[p],
                PushRadius = _definition.RespawnPushRadius * 0.6f, PushSpeed = _definition.RespawnPushSpeed * 0.6f,
            });
        }

        /// <summary>Closest standing teammate within revive radius, or -1. Standing next to them is enough (no button).</summary>
        int NearestReviver(int downed)
        {
            float best = _definition.ReviveRadius * _definition.ReviveRadius;
            int reviver = -1;
            for (int q = 0; q < PlayerStateTable.Max; q++)
            {
                if (q == downed || !_players.CanAct(q)) continue;
                float dx = _players.X[q] - _players.X[downed];
                float dz = _players.Z[q] - _players.Z[downed];
                float d2 = dx * dx + dz * dz;
                if (d2 > best) continue;
                best = d2;
                reviver = q;
            }
            return reviver;
        }
    }
}
