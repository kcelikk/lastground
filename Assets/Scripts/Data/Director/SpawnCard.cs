using LastGround.Data.Zombies;
using UnityEngine;

namespace LastGround.Data.Director
{
    /// <summary>
    /// One zombie type in the director's deck (TDD_01 §9.6 SpawnCardDefinition): point cost, when it unlocks, how its
    /// weight grows with run time, and how many may be alive at once.
    /// </summary>
    [System.Serializable]
    public struct SpawnCard
    {
        public ZombieDefinition Zombie;
        /// <summary>Spawn points (Walker 1, Runner 2, Exploder 3, Spitter 4, Tank 10).</summary>
        public int Cost;
        public float MinRunSeconds;
        /// <summary>Relative weight over run minutes (x = minutes since unlock).</summary>
        public AnimationCurve Weight;
        /// <summary>Alive + queued cap per player (0 = no cap).</summary>
        public int MaxConcurrentPerPlayer;
        /// <summary>Chance this card becomes an elite once elites are unlocked.</summary>
        [Range(0f, 1f)] public float EliteChance;
    }
}
