namespace LastGround.Gameplay.Combat
{
    /// <summary>A bullet left a muzzle (tracer, muzzle flash, sound). Presentation data.</summary>
    public struct ShotFired
    {
        public byte Shooter;
        public float OriginX;
        public float OriginZ;
        public float EndX;
        public float EndZ;
        /// <summary>First pellet of the trigger pull: muzzle flash and sound play once per shot.</summary>
        public bool FirstPellet;
        public bool Local;
    }
}
