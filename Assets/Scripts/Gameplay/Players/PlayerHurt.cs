namespace LastGround.Gameplay.Players
{
    /// <summary>A player lost health (presentation: shake, haptics, sound, damage vignette).</summary>
    public struct PlayerHurt
    {
        public int Player;
        public float Amount;
        public bool Died;
    }
}
