namespace LastGround.Gameplay.Loot
{
    /// <summary>What a pickup is (TDD_01 §13.1). Coins are team loot; the rest are instanced per player.</summary>
    public enum PickupType : byte
    {
        Coin = 0,
        Medkit = 1,
    }
}
