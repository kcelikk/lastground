using LastGround.Gameplay.Navigation;
using Unity.Mathematics;
using UnityEngine;

namespace LastGround.App.Dev
{
    /// <summary>
    /// Dev only: steps the soak bot towards a goal around walls (fences, buildings) with a breadth-first distance field
    /// over the nav grid, rebuilt only when the goal moves. Real players find their own way.
    /// </summary>
    public sealed class DevPathSeeker
    {
        readonly NavGrid _nav;
        readonly int[] _distance;
        readonly int[] _queue;
        int2 _goal = new int2(int.MinValue, int.MinValue);

        public DevPathSeeker(NavGrid nav)
        {
            _nav = nav;
            _distance = new int[nav.CellCount];
            _queue = new int[nav.CellCount];
        }

        /// <summary>Unit direction from <paramref name="from"/> towards <paramref name="goal"/> along walkable cells.</summary>
        public Vector2 Direction(float2 from, float2 goal)
        {
            int2 target = _nav.CellOf(goal);
            if (!target.Equals(_goal)) Build(target);
            int2 cell = _nav.CellOf(from);
            if (!_nav.InBounds(cell)) return Vector2.zero;
            int best = _distance[cell.y * _nav.Width + cell.x];
            int2 next = cell;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int2 n = cell + new int2(dx, dy);
                    if (!_nav.InBounds(n)) continue;
                    int d = _distance[n.y * _nav.Width + n.x];
                    if (d < 0 || (best >= 0 && d >= best)) continue;
                    best = d;
                    next = n;
                }
            }
            float2 to = _nav.CellCentre(next) - from;
            return math.lengthsq(to) > 1e-4f ? (Vector2)math.normalize(to) : Vector2.zero;
        }

        void Build(int2 goal)
        {
            _goal = goal;
            for (int i = 0; i < _distance.Length; i++) _distance[i] = -1;
            if (!_nav.InBounds(goal)) return;
            int head = 0, tail = 0;
            int start = goal.y * _nav.Width + goal.x;
            _distance[start] = 0;
            _queue[tail++] = start;
            while (head < tail)
            {
                int index = _queue[head++];
                int x = index % _nav.Width, y = index / _nav.Width;
                for (int k = 0; k < 4; k++)
                {
                    int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (nx < 0 || ny < 0 || nx >= _nav.Width || ny >= _nav.Height) continue;
                    int n = ny * _nav.Width + nx;
                    if (_distance[n] >= 0 || _nav.Walkable[n] == 0) continue;
                    _distance[n] = _distance[index] + 1;
                    _queue[tail++] = n;
                }
            }
        }
    }
}
