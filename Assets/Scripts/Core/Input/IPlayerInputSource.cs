namespace LastGround.Core.Input
{
    /// <summary>Provides the local player's input for the current frame.</summary>
    public interface IPlayerInputSource
    {
        PlayerInputFrame Current { get; }
    }
}
