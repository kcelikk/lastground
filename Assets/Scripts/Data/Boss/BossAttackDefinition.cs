using LastGround.Data.Crowd;
using UnityEngine;

namespace LastGround.Data.Boss
{
    /// <summary>
    /// One boss attack (TDD_01 §10 <c>BossAttackDefinition</c>): telegraph shape and time (mobile: ≥ 0.8 s), damage,
    /// reach, cooldown and the phases it is used in. Only the fields of its <see cref="Kind"/> are read.
    /// Read-only at runtime.
    /// </summary>
    [CreateAssetMenu(menuName = "LastGround/Boss/Attack")]
    public sealed class BossAttackDefinition : ScriptableObject
    {
        public const float MinTelegraph = 0.8f;

        public string Id = "ground_slam";
        public BossAttackKind Kind;
        public BossAttackShape Shape;
        /// <summary>Animation slot played from the windup on (boss body clip table).</summary>
        public CrowdClipId Clip = CrowdClipId.Attack;

        [Header("Timing")]
        [Min(MinTelegraph)] public float TelegraphSeconds = 1.1f;
        public float ActiveSeconds = 0.3f;
        public float RecoverySeconds = 0.8f;
        public float Cooldown = 6f;

        [Header("Choice")]
        /// <summary>Phases that may pick it: bit 0 = Phase 1, bit 1 = Phase 2, bit 2 = Enraged.</summary>
        public int PhaseMask = 7;
        public float Weight = 1f;
        /// <summary>Distance to the target within which it may be chosen.</summary>
        public Vector2 Range = new Vector2(0f, 8f);

        [Header("Hit")]
        public float Damage = 30f;
        /// <summary>Circle radius (slam, debris) or half width of the charge line.</summary>
        public float Radius = 6f;
        public float Knockback = 8f;

        [Header("Ground Slam: shock ring")]
        public float RingDamage = 15f;
        public float RingMaxRadius = 12f;
        public float RingSpeed = 10f;
        /// <summary>Ring thickness: players inside the band when it passes are hit (standing still inside the slam circle is not safe).</summary>
        public float RingWidth = 1.2f;

        [Header("Charge")]
        public float Length = 16f;
        public float ChargeSpeed = 16f;
        public float WallStunSeconds = 2f;

        [Header("Prop Throw")]
        public float FlightSeconds = 0.9f;

        [Header("Summon Scream")]
        public int SummonCount = 6;
        /// <summary>Zombie TypeIndex of the summoned pack (Runner).</summary>
        public byte SummonType = 1;
    }
}
