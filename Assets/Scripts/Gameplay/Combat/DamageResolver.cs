using LastGround.Data.Weapons;
using LastGround.Gameplay.Upgrades;

namespace LastGround.Gameplay.Combat
{
    /// <summary>
    /// Outgoing damage (TDD_01 §5.4): upgraded base × crit. Damage types and resistances join with zombie types (M6).
    /// Pure and deterministic: the shooter's prediction and the host use the same function and the same build.
    /// </summary>
    public static class DamageResolver
    {
        public static float Resolve(in WeaponStats weapon, uint shotSeed, int pellet, out bool crit)
        {
            crit = ShotRng.IsCrit(shotSeed, pellet, weapon.CritChance);
            return weapon.Damage * (crit ? weapon.CritMultiplier : 1f);
        }

        public static float Resolve(WeaponDefinition weapon, uint shotSeed, int pellet, out bool crit)
        {
            WeaponStats stats = WeaponStats.From(weapon, null);
            return Resolve(stats, shotSeed, pellet, out crit);
        }
    }
}
