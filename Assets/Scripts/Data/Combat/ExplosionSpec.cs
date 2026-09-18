namespace LastGround.Data.Combat
{
    /// <summary>
    /// One area-of-effect blast (TDD_01 §5.6 ExplosionSystem): grenades, Exploders, Volatile elites. Damage falls off
    /// linearly from the centre to <see cref="EdgeDamageScale"/> at the radius.
    /// </summary>
    [System.Serializable]
    public struct ExplosionSpec
    {
        public float Radius;
        public float Damage;
        [UnityEngine.Range(0f, 1f)] public float EdgeDamageScale;
        /// <summary>Outward velocity impulse for zombies at the centre, m/s.</summary>
        public float Knockback;
        public float StunSeconds;
        /// <summary>Damage scale for players (0 = harmless to players). Player-thrown blasts only hurt the thrower (TDD_01 §5.4).</summary>
        [UnityEngine.Range(0f, 1f)] public float PlayerDamageScale;

        public bool IsValid => Radius > 0f;
    }
}
