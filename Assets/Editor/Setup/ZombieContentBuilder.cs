using LastGround.Data.Combat;
using LastGround.Data.Director;
using LastGround.Data.Zombies;
using UnityEditor;
using UnityEngine;

namespace LastGround.EditorTools.Setup
{
    /// <summary>
    /// M6 zombie types (TDD_01 §8.5) and the director's spawn deck (§9.6, unlock times from §2.2). TypeIndex values are
    /// wire ids: append, never renumber. Only missing assets are created; existing ones keep hand-tuned values.
    /// </summary>
    static class ZombieContentBuilder
    {
        const string Zombies = "Assets/ScriptableObjects/Zombies";
        public const string DeckPath = "Assets/ScriptableObjects/Director/DIR_SpawnDeck.asset";

        public static ZombieDefinition[] Build(ProjectileDefinition spit, EliteModifierDefinition[] elites)
        {
            var walker = AssetDatabase.LoadAssetAtPath<ZombieDefinition>(CombatContentBuilder.WalkerPath);
            var runner = Zombie("runner", 1, ZombieBehaviour.Runner, z =>
            {
                z.MaxHealth = 30f;
                z.Xp = 2;
                z.CoinChance = 0.4f;
                z.MinSpeed = 3.4f;
                z.MaxSpeed = 4f;
                z.AttackDamage = 4f;
                z.AttackWindup = 0.35f;
                z.AttackCooldown = 1.2f;
                z.Radius = 0.4f;
            });
            var tank = Zombie("tank", 2, ZombieBehaviour.Tank, z =>
            {
                z.MaxHealth = 600f;
                z.Xp = 12;
                z.CoinChance = 1f;
                z.CoinValue = 5;
                z.MinSpeed = 1f;
                z.MaxSpeed = 1.2f;
                z.AttackDamage = 18f;
                z.AttackWindup = 0.7f;
                z.AttackCooldown = 2.2f;
                z.AttackReachGrace = 0.6f;
                z.KnockbackScale = 0f;
                z.Mass = 6f;
                z.Radius = 0.8f;
            });
            var spitter = Zombie("spitter", 3, ZombieBehaviour.Spitter, z =>
            {
                z.MaxHealth = 55f;
                z.Xp = 4;
                z.CoinChance = 0.5f;
                z.MinSpeed = 1.6f;
                z.MaxSpeed = 2f;
                z.AttackDamage = 4f;
                z.Projectile = spit;
            });
            var exploder = Zombie("exploder", 4, ZombieBehaviour.Exploder, z =>
            {
                z.MaxHealth = 40f;
                z.Xp = 3;
                z.CoinChance = 0.4f;
                z.MinSpeed = 1.4f;
                z.MaxSpeed = 1.8f;
                z.Radius = 0.5f;
                z.Explosion = new ExplosionSpec
                {
                    Radius = 3.5f, Damage = 60f, EdgeDamageScale = 0.35f, Knockback = 6f, PlayerDamageScale = 0.5f,
                };
            });

            WeaponContentBuilder.Ensure<SpawnDeckDefinition>(DeckPath, d =>
            {
                d.Cards = new[]
                {
                    Card(walker, cost: 1, unlock: 0f, weight: 10f, weightLate: 10f, maxPerPlayer: 0, elite: 0.02f),
                    Card(runner, cost: 2, unlock: 180f, weight: 2f, weightLate: 4f, maxPerPlayer: 12, elite: 0.05f),
                    Card(spitter, cost: 4, unlock: 180f, weight: 1f, weightLate: 2f, maxPerPlayer: 3, elite: 0.08f),
                    Card(exploder, cost: 3, unlock: 420f, weight: 1.5f, weightLate: 3f, maxPerPlayer: 4, elite: 0.05f),
                    Card(tank, cost: 10, unlock: 420f, weight: 0.5f, weightLate: 1.2f, maxPerPlayer: 1, elite: 0.15f),
                };
                d.EliteModifiers = elites;
            });
            return new[] { walker, runner, tank, spitter, exploder };
        }

        static ZombieDefinition Zombie(string id, byte typeIndex, ZombieBehaviour behaviour, System.Action<ZombieDefinition> init)
        {
            string name = System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(id);
            return WeaponContentBuilder.Ensure<ZombieDefinition>(Zombies + "/ZMB_" + name + ".asset", z =>
            {
                z.Id = id;
                z.DisplayNameKey = "zombie." + id;
                z.TypeIndex = typeIndex;
                z.Behaviour = behaviour;
                init(z);
            });
        }

        /// <summary>Weight ramps from <paramref name="weight"/> at unlock to <paramref name="weightLate"/> ten minutes later.</summary>
        static SpawnCard Card(ZombieDefinition zombie, int cost, float unlock, float weight, float weightLate, int maxPerPlayer, float elite)
        {
            return new SpawnCard
            {
                Zombie = zombie,
                Cost = cost,
                MinRunSeconds = unlock,
                Weight = AnimationCurve.Linear(0f, weight, 10f, weightLate),
                MaxConcurrentPerPlayer = maxPerPlayer,
                EliteChance = elite,
            };
        }
    }
}
