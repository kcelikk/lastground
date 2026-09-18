using LastGround.Data.Weapons;

namespace LastGround.Gameplay.Combat
{
    /// <summary>
    /// Outgoing damage (TDD_01 §5.4): base × crit. Stat bonuses, damage types and resistances join with upgrades
    /// (M5) and zombie types (M6). Pure and deterministic: the shooter's prediction uses the same function.
    /// </summary>
    public static class DamageResolver
    {
        public static float Resolve(WeaponDefinition weapon, uint shotSeed, int pellet, out bool crit)
        {
            crit = ShotRng.IsCrit(shotSeed, pellet, weapon.CritChance);
            return weapon.Damage * (crit ? weapon.CritMultiplier : 1f);
        }
    }
}
