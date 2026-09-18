namespace LastGround.Data.Crowd
{
    /// <summary>
    /// Animation slots every crowd body provides. Values match the 3-bit animState of snapshots (TDD_02 §17.2),
    /// so replicated state maps directly to a clip.
    /// </summary>
    public enum CrowdClipId : byte
    {
        Idle = 0,
        Walk = 1,
        Run = 2,
        Attack = 3,
        Hit = 4,
        Crawl = 5,
        Death = 6,
    }
}
