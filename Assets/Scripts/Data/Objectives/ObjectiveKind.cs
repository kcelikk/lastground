namespace LastGround.Data.Objectives
{
    /// <summary>
    /// Map events with an objective (TDD_01 §12.2, §12.4, D-019). Wire byte of ObjectiveState: append only.
    /// Boss and Extraction arrive with M8.
    /// </summary>
    public enum ObjectiveKind : byte
    {
        /// <summary>Kill zombies inside a region (counter).</summary>
        ClearArea = 0,
        /// <summary>A crate is dropped at an anchor after a warning; stand next to it to open it (instanced loot).</summary>
        SupplyDrop = 1,
        /// <summary>A locked cache guarded by an elite; kill the guard, then open it (a weapon for everyone).</summary>
        WeaponCache = 2,
        /// <summary>Hold the generator under pressure; the lights come on and a turret fights for a while.</summary>
        PowerGenerator = 3,
        /// <summary>Defend a circle for a while; progress only while someone is inside. Brings the dead back.</summary>
        RescueSignal = 4,
        /// <summary>A marked elite roams the region; kill it.</summary>
        EliteHunt = 5,
    }
}
