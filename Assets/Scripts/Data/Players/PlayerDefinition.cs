using UnityEngine;

namespace LastGround.Data.Players
{
    /// <summary>
    /// Base player stats (TDD_01 §14). Every character shares them (D-005); run upgrades modify copies at runtime.
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

        [Header("Death (M4: respawn in place; downed/revive arrives in M5)")]
        public float RespawnDelay = 5f;
        public float RespawnInvulnerability = 3f;
        /// <summary>Zombies within this radius are pushed away when the player gets back up.</summary>
        public float RespawnPushRadius = 5f;
        public float RespawnPushSpeed = 10f;
    }
}
