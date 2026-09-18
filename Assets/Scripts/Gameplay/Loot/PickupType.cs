namespace LastGround.Gameplay.Loot
{
    /// <summary>What a pickup is (TDD_01 §13.1). Coins are team loot; the rest are instanced per player.</summary>
    public enum PickupType : byte
    {
        Coin = 0,
        Medkit = 1,
        Ammo = 2,
        Grenade = 3,
        /// <summary>Value = the weapon's NetIndex; taking it replaces the primary slot.</summary>
        Weapon = 4,
    }
}
