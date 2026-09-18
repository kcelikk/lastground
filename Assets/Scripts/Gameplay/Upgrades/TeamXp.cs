namespace LastGround.Gameplay.Upgrades
{
    /// <summary>What every device shows about team progress (host writes; clients receive TeamXp).</summary>
    public sealed class TeamXp
    {
        public int Xp;
        public int XpToNext = 1;
        public int Level = 1;
    }
}
