namespace LastGround.Data.Boss
{
    /// <summary>Boss attacks (TDD_01 §10). Values travel on the wire in BossAttack messages: append only.</summary>
    public enum BossAttackKind : byte
    {
        /// <summary>Circle around the boss, then a shock ring rolls outwards.</summary>
        GroundSlam = 0,
        /// <summary>Line dash; walls stun the boss, barrels in the path blow up.</summary>
        Charge = 1,
        /// <summary>Rips debris out of the ground and lobs it at a player's position.</summary>
        PropThrow = 2,
        /// <summary>Roar: a pack of Runners joins the fight.</summary>
        SummonScream = 3,
    }
}
