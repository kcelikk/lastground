namespace LastGround.Gameplay.Crowd
{
    /// <summary>
    /// Read-only crowd view for rendering: the host renders its simulation, clients render their replica
    /// (TDD_02 §27 IZombieRenderSource). Arrays are indexed by slot; skip slots where Alive is false.
    /// </summary>
    public interface ICrowdRenderSource
    {
        int Capacity { get; }
        int ActiveCount { get; }
        bool[] Alive { get; }
        float[] X { get; }
        float[] Z { get; }

        /// <summary>Yaw in degrees around +Y.</summary>
        float[] Yaw { get; }

        /// <summary>Animation state per slot (values of Data.Crowd.CrowdClipId / snapshot animState).</summary>
        byte[] AnimState { get; }
    }
}
