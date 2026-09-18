namespace LastGround.Core.Run
{
    /// <summary>Parameters of the run being loaded, shared by host and clients via LoadRun (TDD_02 §19.4).</summary>
    public sealed class RunLaunch
    {
        public uint Seed;
        public string MapId;
    }
}
