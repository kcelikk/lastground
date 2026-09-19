namespace LastGround.Meta
{
    /// <summary>Outcome of an unlock attempt.</summary>
    public enum UnlockResult
    {
        Unlocked,
        AlreadyOwned,
        NotEnoughScrap,
        MissingRequirement,
        NotForSale,
    }
}
