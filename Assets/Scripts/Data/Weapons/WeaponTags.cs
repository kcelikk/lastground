namespace LastGround.Data.Weapons
{
    /// <summary>Weapon traits for upgrade filters and presentation (TDD_01 §6.1).</summary>
    [System.Flags]
    public enum WeaponTags
    {
        None = 0,
        Ballistic = 1 << 0,
        Automatic = 1 << 1,
        Shotgun = 1 << 2,
        Precision = 1 << 3,
        Heavy = 1 << 4,
        Explosive = 1 << 5,
    }
}
