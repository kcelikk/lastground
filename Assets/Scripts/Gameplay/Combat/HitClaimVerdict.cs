namespace LastGround.Gameplay.Combat
{
    /// <summary>Result of checking a hit claim on the host (TDD_01 §5.2).</summary>
    public enum HitClaimVerdict : byte
    {
        Accepted = 0,
        UnknownShooter,
        ShooterDead,
        UnknownWeapon,
        TargetGone,
        OutOfRange,
        FarFromTarget,
        NoLineOfSight,
        RateLimited,
        TooManyClaims,
        Count,
    }
}
