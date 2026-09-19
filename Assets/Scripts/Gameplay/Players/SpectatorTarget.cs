namespace LastGround.Gameplay.Players
{
    /// <summary>
    /// Spectating (M10, TDD_01 §14.5): while the local player is dead, the camera follows a teammate who is still in
    /// the game; <see cref="Next"/> cycles through them. Back to the own body as soon as the local player returns.
    /// </summary>
    public sealed class SpectatorTarget
    {
        readonly PlayerStateTable _players;

        public SpectatorTarget(PlayerStateTable players)
        {
            _players = players;
        }

        /// <summary>The teammate being watched, or -1 (following the local player).</summary>
        public int Target { get; private set; } = -1;

        /// <summary>Call once per frame before the camera reads <see cref="Target"/>.</summary>
        public void Update()
        {
            int me = _players.Local.IsValid ? _players.Local.Value : -1;
            if (me < 0 || !_players.IsDead(me))
            {
                Target = -1;
                return;
            }
            if (!Watchable(Target)) Target = Following(Target < 0 ? me : Target);
        }

        /// <summary>Watch the next teammate (tap on the spectator bar).</summary>
        public void Next()
        {
            if (Target >= 0) Target = Following(Target);
        }

        bool Watchable(int p)
        {
            return p >= 0 && p != _players.Local.Value && _players.Active[p] && !_players.IsDead(p);
        }

        int Following(int from)
        {
            for (int step = 1; step <= PlayerStateTable.Max; step++)
            {
                int p = (from + step) % PlayerStateTable.Max;
                if (Watchable(p)) return p;
            }
            return -1;
        }
    }
}
