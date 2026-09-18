using LastGround.Data.Combat;
using LastGround.Data.Weapons;
using UnityEditor;
using UnityEngine;

namespace LastGround.EditorTools.Setup
{
    /// <summary>
    /// M6 weapons (TDD_01 §6.4), projectiles and elite modifiers under Assets/ScriptableObjects, and the combat
    /// catalog that indexes them by wire id. Only missing assets are created; existing ones keep hand-tuned values.
    /// NetIndex values are wire ids: append, never renumber.
    /// </summary>
    static class WeaponContentBuilder
    {
        const string Weapons = "Assets/ScriptableObjects/Weapons";
        const string Combat = "Assets/ScriptableObjects/Combat";
        public const string CatalogPath = Combat + "/CMB_Catalog.asset";
        public const string GrenadePath = Combat + "/PRJ_FragGrenade.asset";
        public const string SpitPath = Combat + "/PRJ_Spit.asset";

        public static void Build()
        {
            var weapons = new[]
            {
                AssetDatabase.LoadAssetAtPath<WeaponDefinition>(CombatContentBuilder.WeaponPath),
                Weapon("pistol", 1, w =>
                {
                    Set(w, damage: 18f, rate: 3f, mag: 12, reload: 1.2f, range: 22f, spread: 2f, crit: 0.05f, critMult: 2f, pen: 0);
                    w.Slot = WeaponSlot.Sidearm;
                    w.Tags = WeaponTags.Ballistic;
                    w.InfiniteReserve = true;
                    w.Knockback = 1f;
                }),
                Weapon("smg", 2, w =>
                {
                    Set(w, damage: 9f, rate: 12f, mag: 30, reload: 1.6f, range: 18f, spread: 6f, crit: 0.05f, critMult: 2f, pen: 0);
                    w.MaxReserveAmmo = 180;
                    w.Knockback = 0.6f;
                }),
                Weapon("shotgun", 3, w =>
                {
                    Set(w, damage: 8f, rate: 1.2f, mag: 6, reload: 2.4f, range: 12f, spread: 18f, crit: 0.05f, critMult: 2f, pen: 0);
                    w.FireMode = WeaponFireMode.Pellet;
                    w.PelletCount = 8;
                    w.Tags = WeaponTags.Ballistic | WeaponTags.Shotgun;
                    w.MaxReserveAmmo = 36;
                    w.Knockback = 3.5f;
                    w.AimAssistConeDeg = 14f;
                }),
                Weapon("sniper", 4, w =>
                {
                    Set(w, damage: 120f, rate: 0.9f, mag: 5, reload: 2.6f, range: 45f, spread: 0f, crit: 0.25f, critMult: 2.5f, pen: 4);
                    w.Tags = WeaponTags.Ballistic | WeaponTags.Precision;
                    w.MaxReserveAmmo = 30;
                    w.Knockback = 2.5f;
                    w.CameraZoomPct = 0.08f;
                    w.AimAssistConeDeg = 6f;
                    w.AimAssistStrength = 0.75f;
                }),
                Weapon("machine_gun", 5, w =>
                {
                    Set(w, damage: 14f, rate: 10f, mag: 100, reload: 4f, range: 28f, spread: 5f, crit: 0.05f, critMult: 2f, pen: 1);
                    w.Tags = WeaponTags.Ballistic | WeaponTags.Automatic | WeaponTags.Heavy;
                    w.MaxReserveAmmo = 300;
                    w.MoveSpeedMultiplierWhileFiring = 0.8f;
                    w.AmmoPickupFraction = 0.25f;
                }),
            };

            ProjectSetup.EnsureFolder(Combat);
            var grenade = Ensure<ProjectileDefinition>(GrenadePath, p =>
            {
                p.Id = "frag_grenade";
                p.DisplayNameKey = "projectile.frag_grenade";
                p.NetIndex = 0;
                p.Motion = ProjectileMotion.Arc;
                p.Explosion = new ExplosionSpec
                {
                    Radius = 4.5f, Damage = 150f, EdgeDamageScale = 0.3f, Knockback = 7f, StunSeconds = 1.2f, PlayerDamageScale = 0.25f,
                };
            });
            var spit = Ensure<ProjectileDefinition>(SpitPath, p =>
            {
                p.Id = "spit";
                p.DisplayNameKey = "projectile.spit";
                p.NetIndex = 1;
                p.Motion = ProjectileMotion.Straight;
                p.Speed = 11f;
                p.MaxRange = 14f;
                p.HitRadius = 0.55f;
                p.ImpactDamage = 8f;
                p.SlowMultiplier = 0.6f;
                p.SlowSeconds = 2f;
                p.Color = new Color(0.45f, 0.85f, 0.15f);
                p.Size = 0.3f;
            });

            var elites = new[]
            {
                Elite("armored", 1, new Color(0.55f, 0.75f, 1f), e =>
                {
                    e.HealthMultiplier = 4f;
                    e.DamageTakenMultiplier = 0.6f;
                    e.KnockbackImmune = true;
                }),
                Elite("fast", 2, new Color(1f, 0.9f, 0.3f), e =>
                {
                    e.HealthMultiplier = 3f;
                    e.SpeedMultiplier = 1.45f;
                }),
                Elite("toxic", 3, new Color(0.45f, 1f, 0.3f), e =>
                {
                    e.HealthMultiplier = 3.5f;
                    e.AttackSlowMultiplier = 0.55f;
                    e.AttackSlowSeconds = 2.5f;
                    e.AttackDamageMultiplier = 1.3f;
                }),
                Elite("volatile", 4, new Color(1f, 0.45f, 0.1f), e =>
                {
                    e.HealthMultiplier = 3f;
                    e.DeathExplosion = new ExplosionSpec
                    {
                        Radius = 4f, Damage = 45f, EdgeDamageScale = 0.4f, Knockback = 5f, PlayerDamageScale = 1f,
                    };
                }),
            };

            var catalog = Ensure<CombatCatalog>(CatalogPath, c =>
            {
                c.StartPrimary = weapons[0];
                c.StartSidearm = weapons[1];
                c.Grenade = grenade;
            });
            catalog.Weapons = weapons;
            catalog.Projectiles = new[] { grenade, spit };
            catalog.Elites = elites;
            catalog.Zombies = ZombieContentBuilder.Build(spit, elites);
            EditorUtility.SetDirty(catalog);
        }

        static WeaponDefinition Weapon(string id, byte netIndex, System.Action<WeaponDefinition> init)
        {
            string name = string.Concat(System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(id.Replace('_', ' ')).Split(' '));
            return Ensure<WeaponDefinition>(Weapons + "/WPN_" + name + ".asset", w =>
            {
                w.Id = id;
                w.DisplayNameKey = "weapon." + id;
                w.NetIndex = netIndex;
                init(w);
            });
        }

        static EliteModifierDefinition Elite(string id, byte netIndex, Color glow, System.Action<EliteModifierDefinition> init)
        {
            return Ensure<EliteModifierDefinition>(Combat + "/ELT_" + id + ".asset", e =>
            {
                e.Id = id;
                e.DisplayNameKey = "elite." + id;
                e.NetIndex = netIndex;
                e.Glow = glow;
                init(e);
            });
        }

        static void Set(WeaponDefinition w, float damage, float rate, int mag, float reload, float range, float spread, float crit,
            float critMult, int pen)
        {
            w.Damage = damage;
            w.FireRate = rate;
            w.MagazineSize = mag;
            w.ReloadTime = reload;
            w.Range = range;
            w.SpreadDeg = spread;
            w.CritChance = crit;
            w.CritMultiplier = critMult;
            w.Penetration = pen;
        }

        internal static T Ensure<T>(string path, System.Action<T> init) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            ProjectSetup.EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
            var asset = ScriptableObject.CreateInstance<T>();
            init?.Invoke(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
