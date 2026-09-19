namespace LastGround.Gameplay.Extraction
{
    /// <summary>Landing zone state (TDD_01 §2.2). Values are on the wire: append only.</summary>
    public enum ExtractionPhase : byte
    {
        None = 0,
        /// <summary>The landing zone is open: get there and hold it.</summary>
        Open = 1,
        /// <summary>Held long enough: the run ends.</summary>
        Extracted = 2,
        /// <summary>The window closed without the team; the run goes on (shown briefly).</summary>
        Missed = 3,
    }
}
