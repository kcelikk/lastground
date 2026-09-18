namespace LastGround.Core.Tick
{
    /// <summary>
    /// A system updated by <see cref="TickLoop"/>. Systems never implement Unity's Update().
    /// </summary>
    public interface ITickable
    {
        /// <summary>
        /// Called once per phase execution. <paramref name="dt"/> is the fixed sim step for sim phases
        /// and the frame delta for frame phases. <paramref name="tick"/> is the current sim tick.
        /// </summary>
        void Tick(float dt, uint tick);
    }
}
