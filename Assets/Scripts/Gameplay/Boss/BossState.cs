using LastGround.Core.Events;

namespace LastGround.Gameplay.Boss
{
    /// <summary>
    /// Every device's view of the boss (TDD_01 §10): phase, crowd slot of its body, health, appearance number, whether
    /// it is stunned, and a channel of attack windups for telegraphs. The host's <see cref="BossController"/> writes
    /// it; clients receive it through replication.
    /// </summary>
    public sealed class BossState
    {
        public BossPhase Phase;
        /// <summary>Crowd slot of the body (same index on every device), -1 when none.</summary>
        public int Slot = -1;
        public float Health;
        public float MaxHealth;
        /// <summary>0 for the first appearance this run, then 1, 2… (returns are tougher).</summary>
        public byte Appearance;
        /// <summary>Hit a wall while charging: dazed, open to the whole team.</summary>
        public bool Stunned;
        /// <summary>Times a boss was defeated this run (extraction windows follow).</summary>
        public int Defeats;
        public int Version;

        public readonly EventChannel<BossAttackStarted> Attacks = new EventChannel<BossAttackStarted>(8);

        public bool Active => Phase >= BossPhase.Intro && Phase <= BossPhase.Enraged;
        public float HealthFraction => MaxHealth > 0f ? Health / MaxHealth : 0f;

        public void Set(BossPhase phase, int slot, float health, float maxHealth, byte appearance, bool stunned, int defeats)
        {
            if (Phase == phase && Slot == slot && Health == health && MaxHealth == maxHealth && Appearance == appearance
                && Stunned == stunned && Defeats == defeats) return;
            Phase = phase;
            Slot = slot;
            Health = health;
            MaxHealth = maxHealth;
            Appearance = appearance;
            Stunned = stunned;
            Defeats = defeats;
            Version++;
        }
    }
}
