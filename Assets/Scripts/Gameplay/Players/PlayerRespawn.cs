namespace LastGround.Gameplay.Players
{
    /// <summary>A player got back up at this position.</summary>
    public struct PlayerRespawn
    {
        public int Player;
        public float X;
        public float Z;
        /// <summary>Zombies within this radius are pushed away at this speed.</summary>
        public float PushRadius;
        public float PushSpeed;
    }
}
