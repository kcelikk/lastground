namespace LastGround.Gameplay.Run
{
    /// <summary>
    /// Whether the run is over and how it went. The host sets it when the team wipes (M5) or extracts (M8);
    /// clients receive it through RunEnd replication. Simulation systems stop once <see cref="Ended"/> is set.
    /// </summary>
    public sealed class RunOutcome
    {
        public bool Ended { get; private set; }
        public RunResult Result { get; private set; }

        public void End(in RunResult result)
        {
            if (Ended) return;
            Ended = true;
            Result = result;
        }
    }
}
