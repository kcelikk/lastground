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
        /// <summary>Hits set zombies on fire for this much damage per second (M6).</summary>
        BurnDps = 12,
        /// <summary>Hits slow zombies by this percentage (M6).</summary>
        SlowOnHitPct = 13,
        /// <summary>Percent chance a hit stuns the zombie (M6).</summary>
        StunChancePct = 14,
        /// <summary>M9 perks: revive progress speed, %.</summary>
        ReviveSpeedPct = 15,
        /// <summary>M9 perks: extra grenades at the start of a run.</summary>
        BonusGrenades = 16,
        /// <summary>M9 perks: reserve ammo of new weapons, %.</summary>
        ReserveAmmoPct = 17,
        Count = 18,
    }
}
