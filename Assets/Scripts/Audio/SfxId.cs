namespace LastGround.Audio
{
    /// <summary>Sound effects of the combat mix (M4, M6: heavy shots, blasts, spit, fuse beep, weapon swap; M8: boss, rotor).</summary>
    public enum SfxId
    {
        Gunshot = 0,
        ZombieHit = 1,
        ZombieDeath = 2,
        PlayerHurt = 3,
        HeavyShot = 4,
        Explosion = 5,
        Spit = 6,
        FuseBeep = 7,
        Swap = 8,
        /// <summary>M8 boss: intro, summon and charge windup.</summary>
        BossRoar = 9,
        /// <summary>M8 boss: ground slam impact.</summary>
        BossSlam = 10,
        /// <summary>M8 extraction: one rotor blade pass (repeated while the zone is held).</summary>
        RotorThump = 11,
        Count = 12,
    }
}
