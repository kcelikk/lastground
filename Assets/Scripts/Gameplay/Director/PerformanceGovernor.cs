namespace LastGround.Gameplay.Director
{
    /// <summary>
    /// Host load guard (TDD_01 §9.5, TDD_03 §G): when the host's frame time runs over its target, the director's
    /// spawn rate and max alive scale down (to 0.5), and recover slowly when there is headroom again.
    /// </summary>
    public sealed class PerformanceGovernor
    {
        const float Min = 0.5f;
        const float DropPerSecond = 0.08f;
        const float RecoverPerSecond = 0.02f;

        float _average;

        public PerformanceGovernor(float targetFrameSeconds)
        {
            TargetFrameSeconds = targetFrameSeconds;
            _average = targetFrameSeconds;
        }

        public float TargetFrameSeconds { get; set; }

        /// <summary>0.5 … 1.</summary>
        public float Multiplier { get; private set; } = 1f;

        /// <summary>Call once per rendered frame with the unscaled frame time.</summary>
        public void Report(float frameSeconds)
        {
            _average += (frameSeconds - _average) * 0.05f;
            if (_average > TargetFrameSeconds * 1.15f) Multiplier -= DropPerSecond * frameSeconds;
            else if (_average < TargetFrameSeconds * 1.05f) Multiplier += RecoverPerSecond * frameSeconds;
            if (Multiplier < Min) Multiplier = Min;
            if (Multiplier > 1f) Multiplier = 1f;
        }
    }
}
