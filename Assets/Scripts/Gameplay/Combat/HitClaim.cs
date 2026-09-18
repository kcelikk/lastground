namespace LastGround.Gameplay.Combat
{
    /// <summary>
    /// "My bullet hit this zombie" (TDD_01 §5.2). Sent by the shooter's device, checked by the host
    /// (<see cref="HitClaimValidator"/>) before any damage is applied.
    /// </summary>
    public struct HitClaim
    {
        public byte Shooter;
        public ushort ShotSeq;
        public byte Weapon;
        public byte Pellet;
        /// <summary>0 = first zombie on the bullet's path, 1 = the next one after piercing, …</summary>
        public byte Pierce;
        public ushort Slot;
        public byte Generation;
        /// <summary>Where the shooter saw the zombie (its interpolated replica position).</summary>
        public float HitX;
        public float HitZ;
    }
}
