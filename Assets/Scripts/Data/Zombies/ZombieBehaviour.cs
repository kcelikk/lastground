namespace LastGround.Data.Zombies
{
    /// <summary>Movement and attack logic of a zombie type (TDD_01 §8.5); the sim switches on it inside Burst jobs.</summary>
    public enum ZombieBehaviour : byte
    {
        Walker = 0,
        Runner = 1,
        Tank = 2,
        Spitter = 3,
        Exploder = 4,
    }
}
