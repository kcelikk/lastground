namespace LastGround.Core.Tick
{
    public static class TickPhaseInfo
    {
        public const int Count = 9;

        /// <summary>True for phases that run on the fixed simulation step.</summary>
        public static bool IsSimPhase(TickPhase phase)
        {
            return phase >= TickPhase.Director && phase <= TickPhase.NetSend;
        }
    }
}
