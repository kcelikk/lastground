namespace LastGround.Data.Upgrades
{
    /// <summary>Stats run upgrades can change (TDD_01 §7.1). Append only: the numbers index stat arrays.</summary>
    public enum StatId : byte
    {
        MaxHealth = 0,
        MoveSpeedPct = 1,
        DamagePct = 2,
        FireRatePct = 3,
        ReloadSpeedPct = 4,
        MagazinePct = 5,
        CritChance = 6,
        CritDamagePct = 7,
        Pierce = 8,
        DamageReductionPct = 9,
        PickupRadiusPct = 10,
        HealOnKill = 11,
        Count = 12,
    }
}
