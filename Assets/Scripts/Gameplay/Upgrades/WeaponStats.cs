using LastGround.Data.Upgrades;
using LastGround.Data.Weapons;
using UnityEngine;

namespace LastGround.Gameplay.Upgrades
{
    /// <summary>
    /// A weapon's numbers after the holder's upgrades (TDD_01 §6.2 WeaponStats: cached, recomputed only when the
    /// build changes). The shooter's device and the host compute the same values from the same build.
    /// </summary>
    public struct WeaponStats
    {
        public float Damage;
        public float FireRate;
        public int MagazineSize;
        public float ReloadTime;
        public float CritChance;
        public float CritMultiplier;
        public int Penetration;
        public float Range;
        public float SpreadDeg;
        public int PelletCount;
        public float Knockback;
        public byte NetIndex;

        public float ShotInterval => FireRate > 0f ? 1f / FireRate : 1f;
        public int MaxClaimsPerShot => Mathf.Max(1, PelletCount) * (Mathf.Max(0, Penetration) + 1);

        public static WeaponStats From(WeaponDefinition weapon, PlayerBuild build)
        {
            float Pct(StatId stat) => build != null ? build.Get(stat) / 100f : 0f;
            return new WeaponStats
            {
                Damage = weapon.Damage * (1f + Pct(StatId.DamagePct)),
                FireRate = weapon.FireRate * (1f + Pct(StatId.FireRatePct)),
                MagazineSize = Mathf.Max(1, Mathf.RoundToInt(weapon.MagazineSize * (1f + Pct(StatId.MagazinePct)))),
                ReloadTime = weapon.ReloadTime / (1f + Pct(StatId.ReloadSpeedPct)),
                CritChance = Mathf.Clamp01(weapon.CritChance + Pct(StatId.CritChance)),
                CritMultiplier = weapon.CritMultiplier + Pct(StatId.CritDamagePct),
                Penetration = weapon.Penetration + (build != null ? Mathf.RoundToInt(build.Get(StatId.Pierce)) : 0),
                Range = weapon.Range,
                SpreadDeg = weapon.SpreadDeg,
                PelletCount = weapon.PelletCount,
                Knockback = weapon.Knockback,
                NetIndex = weapon.NetIndex,
            };
        }
    }
}
