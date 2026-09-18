namespace LastGround.Core.Input
{
    /// <summary>Settings → Controls (TDD_01 §3.4).</summary>
    public enum ControlMode : byte
    {
        /// <summary>Twin-stick: right stick aims, pushing it further fires.</summary>
        Manual = 0,
        /// <summary>The game picks a target and fires; the right stick can still override.</summary>
        AutoAimAutoFire = 1,
    }
}
