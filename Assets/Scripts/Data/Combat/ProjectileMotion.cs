namespace LastGround.Data.Combat
{
    /// <summary>How a projectile travels (TDD_01 §6.3). Both are closed-form so clients replay the flight exactly.</summary>
    public enum ProjectileMotion : byte
    {
        /// <summary>Lobbed to a target point in a fixed flight time (grenade).</summary>
        Arc = 0,
        /// <summary>Constant velocity until it hits a player, a wall or its range (Spitter spit).</summary>
        Straight = 1,
    }
}
