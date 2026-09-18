using LastGround.Gameplay.Players;
using Unity.Mathematics;

namespace LastGround.Gameplay.Zombies
{
    public sealed partial class ZombieWorld
    {
        /// <summary>Nearest active player, with hysteresis so zombies do not flip between two players.</summary>
        void SelectTargets()
        {
            for (int i = 0; i < _capacity; i++)
            {
                if (_alive[i] == 0) continue;
                float2 p = _position[i];
                int best = ZombieSteeringJob.NoTarget;
                float bestDistance = float.MaxValue;
                for (int k = 0; k < PlayerStateTable.Max; k++)
                {
                    if (_playerActive[k] == 0) continue;
                    // Downed players are chased less eagerly (TDD_01 §14.2): they count as ~1.4× further away.
                    float d = math.distancesq(p, _playerPosition[k]) * (_players.IsDowned(k) ? 2f : 1f);
                    if (d < bestDistance)
                    {
                        bestDistance = d;
                        best = k;
                    }
                }
                byte current = _target[i];
                if (current != ZombieSteeringJob.NoTarget && _playerActive[current] != 0 && best != current)
                {
                    float currentDistance = math.distancesq(p, _playerPosition[current]) * (_players.IsDowned(current) ? 2f : 1f);
                    if (bestDistance > currentDistance * 0.64f) best = current; // switch only when 20 % closer
                }
                _target[i] = (byte)best;
            }
        }

        /// <summary>Zombies that made no progress while walking get a sideways nudge (TDD_01 §8.4 anti-stuck).</summary>
        void ResolveStuck()
        {
            for (int i = 0; i < _capacity; i++)
            {
                if (_alive[i] == 0) continue;
                float2 p = _position[i];
                // Knocked-back zombies slow down on purpose; they are not stuck.
                bool walking = _outState[i] == ZombieSteeringJob.StateWalk && _stagger[i] <= 0f;
                // Only zombies on their way count: those queueing in the surround rings are supposed to wait.
                if (walking && math.distance(p, _stuckAnchor[i]) < _tuning.StuckDistance && IsFarFromTarget(i, _tuning.SurroundRange))
                {
                    float angle = _rng.Range(-math.PI, math.PI);
                    _velocity[i] = new float2(math.cos(angle), math.sin(angle)) * _speed[i] * 1.5f;
                    Unstuck++;
                }
                _stuckAnchor[i] = p;
            }
        }

        bool IsFarFromTarget(int i, float distance)
        {
            byte t = _target[i];
            return t == ZombieSteeringJob.NoTarget || math.distance(_position[i], _playerPosition[t]) > distance;
        }
    }
}
