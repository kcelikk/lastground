namespace LastGround.Data.Boss
{
    /// <summary>Ground telegraph drawn during an attack's windup (TDD_01 §10).</summary>
    public enum BossAttackShape : byte
    {
        Circle = 0,
        Line = 1,
        /// <summary>Circle at the target position (thrown debris).</summary>
        TargetCircle = 2,
        None = 3,
    }
}
