using LastGround.Data.Boss;

namespace LastGround.Gameplay.Boss
{
    /// <summary>
    /// An attack's windup began (TDD_01 §10 <c>BossAttackStarted</c>): every device draws the same telegraph from it.
    /// Host publishes it locally and replicates it reliably; <see cref="Elapsed"/> is how much of the telegraph had
    /// already passed when this device learned about it.
    /// </summary>
    public struct BossAttackStarted
    {
        /// <summary>Index into BossDefinition.Attacks.</summary>
        public byte Attack;
        public BossAttackKind Kind;
        public float OriginX, OriginZ;
        public float TargetX, TargetZ;
        public float DirX, DirZ;
        public float Elapsed;
    }
}
