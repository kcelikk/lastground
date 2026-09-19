using System.Collections.Generic;
using LastGround.Data.Combat;
using LastGround.Data.Meta;
using LastGround.Data.Upgrades;
using LastGround.Data.Weapons;
using LastGround.Gameplay.Upgrades;

namespace LastGround.Gameplay.Meta
{
    /// <summary>
    /// Turns lobby meta selections into run state on every device (M9, TDD_01 §14.7): the perk's net-zero stat pair
    /// becomes the build's base, the loadout picks the starting primary and grenades, and the weapons owned by anyone
    /// in the session form the run's drop pool. Nothing here adds power beyond the chosen trade-offs (D-005).
    /// </summary>
    public static class MetaApplier
    {
        public static void ApplyPerk(in PlayerMeta meta, PlayerBuild build, MetaCatalog catalog)
        {
            PerkDefinition perk = catalog != null ? MetaCatalog.At(catalog.Perks, meta.Perk) : null;
            if (perk == null)
            {
                build.SetPerk(StatId.MaxHealth, 0f, StatId.MaxHealth, 0f);
                return;
            }
            build.SetPerk(perk.Plus.Stat, perk.Plus.Value, perk.Minus.Stat, perk.Minus.Value);
        }

        /// <summary>Starting primary and grenades; falls back to the combat catalog's defaults.</summary>
        public static void StartLoadout(in PlayerMeta meta, MetaCatalog catalog, CombatCatalog combat, PlayerBuild build,
            out WeaponDefinition primary, out int grenades)
        {
            LoadoutDefinition loadout = catalog != null ? MetaCatalog.At(catalog.Loadouts, meta.Loadout) : null;
            primary = loadout != null && loadout.Primary != null ? loadout.Primary : combat.StartPrimary;
            grenades = loadout != null ? loadout.Grenades : combat.StartGrenades;
            if (build != null) grenades += (int)build.Get(StatId.BonusGrenades);
            if (grenades > combat.MaxGrenades) grenades = combat.MaxGrenades;
            if (grenades < 0) grenades = 0;
        }

        /// <summary>Default weapons plus every weapon owned by a player in the session.</summary>
        public static WeaponDefinition[] DropPool(IReadOnlyList<PlayerMeta> players, MetaCatalog catalog, WeaponDefinition[] fallback)
        {
            if (catalog == null || catalog.Weapons == null || catalog.Weapons.Length == 0) return fallback;
            int owned = 0;
            for (int p = 0; p < players.Count; p++) owned |= players[p].OwnedWeapons;
            var pool = new List<WeaponDefinition>();
            for (int i = 0; i < catalog.Weapons.Length; i++)
            {
                WeaponUnlock unlock = catalog.Weapons[i];
                if (unlock == null || unlock.Weapon == null || unlock.Weapon.Slot != WeaponSlot.Primary) continue;
                if (unlock.OwnedByDefault || (owned & (1 << i)) != 0) pool.Add(unlock.Weapon);
            }
            return pool.Count > 0 ? pool.ToArray() : fallback;
        }
    }
}
