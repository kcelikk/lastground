namespace LastGround.Data.Weapons
{
    /// <summary>Which of the two loadout slots a weapon lives in (TDD_01 §3.5). Pickups replace the primary.</summary>
    public enum WeaponSlot : byte
    {
        Primary = 0,
        Sidearm = 1,
    }
}
