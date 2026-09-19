using LastGround.Data.Boss;
using LastGround.Data.Crowd;
using LastGround.Data.Zombies;
using UnityEditor;

namespace LastGround.EditorTools.Setup
{
    /// <summary>
    /// M8 boss and extraction content (TDD_01 §2.2, §10; D-021): the Mutant Brute's four attacks, its definition and
    /// the extraction rules. Only missing assets are created; existing ones keep hand-tuned values.
    /// </summary>
    static class BossContentBuilder
    {
        const string Folder = "Assets/ScriptableObjects/Boss";
        public const string BossPath = Folder + "/BOSS_MutantBrute.asset";
        public const string ExtractionPath = Folder + "/EXT_Rules.asset";
        const string BrutePath = "Assets/ScriptableObjects/Zombies/ZMB_Brute.asset";

        public static BossDefinition Build()
        {
            var slam = Attack("ground_slam", a =>
            {
                a.Kind = BossAttackKind.GroundSlam;
                a.Shape = BossAttackShape.Circle;
                a.Clip = CrowdClipId.Attack;
                a.TelegraphSeconds = 1.1f;
                a.RecoverySeconds = 0.9f;
                a.Cooldown = 7f;
                a.Range = new UnityEngine.Vector2(0f, 7f);
                a.Weight = 1.2f;
                a.Damage = 35f;
                a.Radius = 6f;
                a.RingDamage = 15f;
                a.RingMaxRadius = 12f;
                a.RingSpeed = 10f;
            });
            var charge = Attack("charge", a =>
            {
                a.Kind = BossAttackKind.Charge;
                a.Shape = BossAttackShape.Line;
                a.Clip = CrowdClipId.Crawl;
                a.TelegraphSeconds = 1f;
                a.RecoverySeconds = 0.8f;
                a.Cooldown = 9f;
                a.Range = new UnityEngine.Vector2(6f, 22f);
                a.PhaseMask = 6;
                a.Weight = 1f;
                a.Damage = 30f;
                a.Radius = 1.8f;
                a.Length = 18f;
                a.ChargeSpeed = 16f;
                a.WallStunSeconds = 2f;
            });
            var throwing = Attack("prop_throw", a =>
            {
                a.Kind = BossAttackKind.PropThrow;
                a.Shape = BossAttackShape.TargetCircle;
                a.Clip = CrowdClipId.Hit;
                a.TelegraphSeconds = 1.4f;
                a.RecoverySeconds = 0.6f;
                a.Cooldown = 6f;
                a.Range = new UnityEngine.Vector2(8f, 30f);
                a.Weight = 1f;
                a.Damage = 25f;
                a.Radius = 3f;
                a.FlightSeconds = 0.9f;
            });
            var summon = Attack("summon_scream", a =>
            {
                a.Kind = BossAttackKind.SummonScream;
                a.Shape = BossAttackShape.None;
                a.Clip = CrowdClipId.Crawl;
                a.TelegraphSeconds = 1.2f;
                a.RecoverySeconds = 0.5f;
                a.Cooldown = 20f;
                a.Range = new UnityEngine.Vector2(0f, 40f);
                a.Weight = 0.6f;
                a.SummonCount = 5;
                a.SummonType = 1;
            });

            WeaponContentBuilder.Ensure<ExtractionRulesDefinition>(ExtractionPath, null);
            var boss = WeaponContentBuilder.Ensure<BossDefinition>(BossPath, b =>
            {
                b.Zombie = AssetDatabase.LoadAssetAtPath<ZombieDefinition>(BrutePath);
                b.Attacks = new[] { slam, charge, throwing, summon };
            });
            if (boss.Zombie == null)
            {
                boss.Zombie = AssetDatabase.LoadAssetAtPath<ZombieDefinition>(BrutePath);
                EditorUtility.SetDirty(boss);
            }
            return boss;
        }

        public static ExtractionRulesDefinition Rules() => AssetDatabase.LoadAssetAtPath<ExtractionRulesDefinition>(ExtractionPath);

        static BossAttackDefinition Attack(string id, System.Action<BossAttackDefinition> init) =>
            WeaponContentBuilder.Ensure<BossAttackDefinition>(Folder + "/BAT_" + id + ".asset", a =>
            {
                a.Id = id;
                init(a);
            });
    }
}
