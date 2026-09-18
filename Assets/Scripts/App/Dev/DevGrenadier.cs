#if UNITY_EDITOR || DEVELOPMENT_BUILD
using LastGround.Core.Tick;
using LastGround.Gameplay.Combat;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Players;
using LastGround.Input;
using UnityEngine;

namespace LastGround.App.Dev
{
    /// <summary>
    /// Soak-test bot (-lg-grenades): every ~10 s, if the local player has a grenade, throws it at the densest spot of
    /// zombies 5–11 m away (the zombie with the most neighbours within 3 m). Development builds only.
    /// </summary>
    public sealed class DevGrenadier : ITickable
    {
        const float Interval = 10f;
        const float MinRange = 5f, MaxRange = 11f, ClusterRadius = 3f;

        readonly PlayerStateTable _players;
        readonly LoadoutTable _loadouts;
        readonly ICrowdRenderSource _crowd;
        float _timer = Interval;

        public DevGrenadier(PlayerStateTable players, LoadoutTable loadouts, ICrowdRenderSource crowd)
        {
            _players = players;
            _loadouts = loadouts;
            _crowd = crowd;
        }

        public void Tick(float dt, uint tick)
        {
            _timer -= dt;
            if (_timer > 0f) return;
            int me = _players.Local.IsValid ? _players.Local.Value : -1;
            if (me < 0 || !_players.CanAct(me) || _loadouts.Grenades[me] == 0) return;
            float px = _players.X[me], pz = _players.Z[me];
            int best = -1, bestCount = 2;
            for (int i = 0; i < _crowd.Capacity; i++)
            {
                if (!_crowd.Alive[i]) continue;
                float d = Vector2.Distance(new Vector2(_crowd.X[i], _crowd.Z[i]), new Vector2(px, pz));
                if (d < MinRange || d > MaxRange) continue;
                int neighbours = 0;
                for (int k = 0; k < _crowd.Capacity; k++)
                {
                    if (k == i || !_crowd.Alive[k]) continue;
                    float dx = _crowd.X[k] - _crowd.X[i], dz = _crowd.Z[k] - _crowd.Z[i];
                    if (dx * dx + dz * dz < ClusterRadius * ClusterRadius) neighbours++;
                }
                if (neighbours <= bestCount) continue;
                bestCount = neighbours;
                best = i;
            }
            if (best < 0) return;
            _timer = Interval;
            TouchTwinStickInput.DevThrow = new Vector2(_crowd.X[best] - px, _crowd.Z[best] - pz);
        }
    }
}
#endif
