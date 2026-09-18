namespace LastGround.Gameplay.Loot
{
    /// <summary>The team's run coin (TDD_01 §13.1, D-002): one number, the same for everyone.</summary>
    public sealed class TeamWallet
    {
        public int Coins;
        public int Version;

        public void Add(int amount)
        {
            if (amount <= 0) return;
            Coins += amount;
            Version++;
        }
    }
}
