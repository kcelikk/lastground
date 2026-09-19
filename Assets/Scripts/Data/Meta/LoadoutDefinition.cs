using LastGround.Data.Weapons;
using UnityEngine;

namespace LastGround.Data.Meta
{
    /// <summary>Starting loadout (TDD_01 §14.7): primary weapon and starting grenades; every loadout is the same tier.</summary>
    [CreateAssetMenu(menuName = "LastGround/Meta/Loadout")]
    public sealed class LoadoutDefinition : MetaItem
    {
        public WeaponDefinition Primary;
        [Min(0)] public int Grenades = 2;
    }
}
