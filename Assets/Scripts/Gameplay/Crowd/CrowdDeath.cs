namespace LastGround.Gameplay.Crowd
{
    /// <summary>A crowd member died here (corpse, blood, sound). Presentation-only data (TDD_02 §21.6).</summary>
    public struct CrowdDeath
    {
        public int Slot;
        public float X;
        public float Z;
        public float Yaw;
        /// <summary>Zombie type (corpse body).</summary>
        public byte Type;
    }
}
