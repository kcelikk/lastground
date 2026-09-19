using LastGround.Data.Upgrades;
using UnityEditor;
using UnityEngine;

namespace LastGround.EditorTools.Setup
{
    /// <summary>
    /// The run upgrades (TDD_01 §7.8: 12 in M5, three on-hit effects in M6) and the level curve, under Assets/ScriptableObjects/Upgrades.
    /// Catalog order is the network id: append new upgrades, never reorder. Existing assets keep their tuned values;
    /// only missing ones are created.
    /// </summary>
    static class UpgradeContentBuilder
    {
        const string Folder = "Assets/ScriptableObjects/Upgrades";
        public const string CatalogPath = Folder + "/UPG_Catalog.asset";
        public const string LevelCurvePath = Folder + "/UPG_LevelCurve.asset";

        struct Spec
        {
            public string Id;
            public StatId Stat;
            public float[] Values;
            public int MaxStacks;
        }

        static readonly Spec[] Specs =
        {
            new Spec { Id = "hollow_points", Stat = StatId.DamagePct, Values = new[] { 10f, 15f, 22f, 30f }, MaxStacks = 5 },
            new Spec { Id = "rapid_trigger", Stat = StatId.FireRatePct, Values = new[] { 8f, 12f, 17f, 24f }, MaxStacks = 5 },
            new Spec { Id = "quick_hands", Stat = StatId.ReloadSpeedPct, Values = new[] { 15f, 22f, 30f, 40f }, MaxStacks = 4 },
            new Spec { Id = "extended_mag", Stat = StatId.MagazinePct, Values = new[] { 20f, 30f, 45f, 60f }, MaxStacks = 4 },
            new Spec { Id = "deadeye", Stat = StatId.CritChance, Values = new[] { 5f, 7f, 10f, 14f }, MaxStacks = 5 },
            new Spec { Id = "executioner", Stat = StatId.CritDamagePct, Values = new[] { 25f, 40f, 60f, 85f }, MaxStacks = 4 },
            new Spec { Id = "penetrator", Stat = StatId.Pierce, Values = new[] { 1f, 1f, 2f, 2f }, MaxStacks = 3 },
            new Spec { Id = "toughness", Stat = StatId.MaxHealth, Values = new[] { 15f, 22f, 30f, 40f }, MaxStacks = 5 },
            new Spec { Id = "fleet_foot", Stat = StatId.MoveSpeedPct, Values = new[] { 6f, 9f, 12f, 16f }, MaxStacks = 3 },
            new Spec { Id = "kevlar", Stat = StatId.DamageReductionPct, Values = new[] { 5f, 7f, 10f, 13f }, MaxStacks = 4 },
            new Spec { Id = "scavenger", Stat = StatId.PickupRadiusPct, Values = new[] { 25f, 40f, 55f, 75f }, MaxStacks = 3 },
            new Spec { Id = "bloodthirst", Stat = StatId.HealOnKill, Values = new[] { 0.5f, 0.8f, 1.2f, 1.6f }, MaxStacks = 4 },
            // M6: on-hit status effects (TDD_01 §5.6).
            new Spec { Id = "incendiary", Stat = StatId.BurnDps, Values = new[] { 4f, 6f, 9f, 12f }, MaxStacks = 3 },
            new Spec { Id = "crippling", Stat = StatId.SlowOnHitPct, Values = new[] { 15f, 20f, 27f, 35f }, MaxStacks = 2 },
            new Spec { Id = "concussive", Stat = StatId.StunChancePct, Values = new[] { 4f, 6f, 8f, 11f }, MaxStacks = 3 },
        };

        public static void Build()
        {
            ProjectSetup.EnsureFolder(Folder);
            var upgrades = new UpgradeDefinition[Specs.Length];
            for (int i = 0; i < Specs.Length; i++)
            {
                Spec spec = Specs[i];
                string path = Folder + "/UPG_" + spec.Id + ".asset";
                var upgrade = AssetDatabase.LoadAssetAtPath<UpgradeDefinition>(path);
                if (upgrade == null)
                {
                    upgrade = ScriptableObject.CreateInstance<UpgradeDefinition>();
                    upgrade.Id = spec.Id;
                    upgrade.NameKey = "upgrade." + spec.Id + ".name";
                    upgrade.DescriptionKey = "upgrade." + spec.Id + ".desc";
                    upgrade.Stat = spec.Stat;
                    upgrade.Values = spec.Values;
                    upgrade.MaxStacks = spec.MaxStacks;
                    AssetDatabase.CreateAsset(upgrade, path);
                }
                upgrades[i] = upgrade;
            }

            var catalog = AssetDatabase.LoadAssetAtPath<UpgradeCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<UpgradeCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.Upgrades = upgrades;
            EditorUtility.SetDirty(catalog);

            if (AssetDatabase.LoadAssetAtPath<LevelCurveDefinition>(LevelCurvePath) == null)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<LevelCurveDefinition>(), LevelCurvePath);
        }
    }
}
