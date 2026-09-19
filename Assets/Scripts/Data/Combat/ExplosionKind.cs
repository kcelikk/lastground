namespace LastGround.Data.Combat
{
    /// <summary>What blew up (presentation picks the effect; wire byte of the Explosion message).</summary>
    public enum ExplosionKind : byte
    {
        Grenade = 0,
        Exploder = 1,
        Volatile = 2,
    }
}
