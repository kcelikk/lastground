namespace LastGround.Core.Run
{
    /// <summary>Parameters of the run being loaded, shared by host and clients via LoadRun (TDD_02 §19.4).</summary>
    public sealed class RunLaunch
    {
        public uint Seed;
        public string MapId;

        /// <summary>Local benchmark run (dev tools) instead of a game run; never sent over the network.</summary>
        public bool Benchmark;
        public float BenchmarkStepSeconds = 60f;
        public bool QuitAfterBenchmark;
    }
}
