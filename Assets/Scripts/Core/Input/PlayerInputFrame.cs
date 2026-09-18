namespace LastGround.Core.Input
{
    /// <summary>
    /// One frame of player intent, identical for touch, gamepad, bots and tests (TDD_01 §3.1).
    /// Gameplay reads only this struct and never touches devices.
    /// </summary>
    public struct PlayerInputFrame
    {
        /// <summary>Move direction in world XZ (x = right, y = forward), length 0..1.</summary>
        public float MoveX;
        public float MoveY;

        /// <summary>Aim direction; valid when <see cref="AimActive"/> (M4).</summary>
        public float AimX;
        public float AimY;
        public bool AimActive;
        public bool FireHeld;
    }
}
