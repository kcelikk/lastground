using UnityEngine;

namespace LastGround.Data.Players
{
    /// <summary>
    /// Base player stats (TDD_01 §14). Every character shares them (D-005); run upgrades modify copies at runtime.
    /// Includes the downed/revive rules (§14.2).
    /// </summary>
    [CreateAssetMenu(menuName = "LastGround/Players/Player")]
    public sealed class PlayerDefinition : ScriptableObject
    {
        public float MaxHealth = 100f;
        public float MoveSpeed = 5f;

        [Header("Being grabbed (TDD_01 §8.4)")]
        /// <summary>Speed lost per touching zombie.</summary>
        [Range(0f, 1f)] public float SlowPerContact = 0.07f;
        [Range(0f, 1f)] public float MaxContactSlow = 0.35f;
        public float ContactRadius = 1.1f;

        [Header("Downed / revive (TDD_01 §14.2)")]
        public float BleedoutTime = 25f;
        /// <summary>From the Nth down in one life, bleedout runs faster.</summary>
        public int FastBleedoutFromDown = 3;
        public float FastBleedoutScale = 0.5f;
        [Range(0f, 1f)] public float DownedSpeedFactor = 0.3f;
        public float ReviveRadius = 2f;
        public float ReviveTime = 4f;
        /// <summary>Revive progress multiplier while the reviver is being hit.</summary>
        [Range(0f, 1f)] public float HurtReviveFactor = 0.5f;
        /// <summary>Progress lost per second when nobody is reviving.</summary>
        public float ReviveDecayPerSecond = 0.25f;
        [Range(0f, 1f)] public float ReviveHealthFraction = 0.3f;
        public float ReviveInvulnerability = 2f;
        /// <summary>Solo: self-revives per run ("Adrenaline"); without one, going down ends the run.</summary>
        public int SoloAdrenaline = 1;

        [Header("Dead → back in the fight")]
        /// <summary>A dead player returns after this long if a teammate is still standing.</summary>
        public float RespawnDelay = 60f;
        [Range(0f, 1f)] public float RespawnHealthFraction = 0.5f;
        public float RespawnInvulnerability = 3f;
        /// <summary>Zombies within this radius are pushed away when a player gets back up.</summary>
        public float RespawnPushRadius = 5f;
        public float RespawnPushSpeed = 10f;
    }
}
