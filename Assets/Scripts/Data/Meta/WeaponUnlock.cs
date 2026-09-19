using LastGround.Data.Weapons;
using UnityEngine;

namespace LastGround.Data.Meta
{
    /// <summary>
    /// A weapon in the Arsenal (TDD_01 §14.7, D-005): owning it adds it to the starting loadouts and to the run's drop
    /// pool. Weapons are sidegrades — the same power budget, a different play style.
    /// </summary>
    [CreateAssetMenu(menuName = "LastGround/Meta/Weapon Unlock")]
    public sealed class WeaponUnlock : MetaItem
    {
        public WeaponDefinition Weapon;
    }
}
