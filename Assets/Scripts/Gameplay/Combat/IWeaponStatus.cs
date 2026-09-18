namespace LastGround.Gameplay.Combat
{
    /// <summary>Read-only weapon state for the HUD.</summary>
    public interface IWeaponStatus
    {
        int Ammo { get; }
        int MagazineSize { get; }
        bool Reloading { get; }
        /// <summary>0..1 while reloading.</summary>
        float ReloadProgress { get; }
    }
}
