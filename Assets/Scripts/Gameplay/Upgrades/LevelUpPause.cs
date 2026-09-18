namespace LastGround.Gameplay.Upgrades
{
    /// <summary>TDD_01 §7.5: choosing an upgrade may pause a solo run (setting); a co-op run never pauses.</summary>
    public static class LevelUpPause
    {
        public static bool ShouldPause(int activePlayers, bool panelOpen, bool pauseInSoloSetting)
        {
            return activePlayers <= 1 && panelOpen && pauseInSoloSetting;
        }
    }
}
