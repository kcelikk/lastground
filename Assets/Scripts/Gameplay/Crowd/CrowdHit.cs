namespace LastGround.Gameplay.Crowd
{
    /// <summary>
    /// A crowd member was hit (presentation data: blood, flash, damage number, sound). Published by the host for
    /// authoritative hits, and on clients for the local player's predicted hits and other players' hit flags.
    /// </summary>
    public struct CrowdHit
    {
        public int Slot;
        public float X;
        public float Z;
        /// <summary>Shot direction (blood spray), zero when unknown.</summary>
        public float DirX;
        public float DirZ;
        public float Damage;
        public bool Crit;
        /// <summary>Caused by the local player: show a damage number.</summary>
        public bool Local;
    }
}
