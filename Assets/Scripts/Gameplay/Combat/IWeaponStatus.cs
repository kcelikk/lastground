namespace LastGround.Gameplay.Combat
{
    /// <summary>Read-only weapon state for the HUD and the motor.</summary>
    public interface IWeaponStatus
    {
        int Ammo { get; }
        int MagazineSize { get; }
        /// <summary>Spare rounds of the weapon in hand.</summary>
        int Reserve { get; }
        bool InfiniteReserve { get; }
        /// <summary>Weapon in hand and the one in the other slot (null if empty).</summary>
        Data.Weapons.WeaponDefinition Active { get; }
        Data.Weapons.WeaponDefinition Other { get; }
        int Grenades { get; }
        /// <summary>Move speed multiplier right now (heavy weapons slow the shooter while firing).</summary>
        float MoveMultiplier { get; }
        bool Reloading { get; }
        /// <summary>0..1 while reloading.</summary>
        float ReloadProgress { get; }
    }
}
