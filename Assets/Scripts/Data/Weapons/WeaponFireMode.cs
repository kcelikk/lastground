namespace LastGround.Data.Weapons
{
    /// <summary>How a trigger pull turns into hits (TDD_01 §6.3). Projectile and Cone arrive in M6+.</summary>
    public enum WeaponFireMode : byte
    {
        Hitscan = 0,
        Pellet = 1,
    }
}
