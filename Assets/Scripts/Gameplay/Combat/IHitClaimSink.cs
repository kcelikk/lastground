namespace LastGround.Gameplay.Combat
{
    /// <summary>Where a weapon sends its hit claims: the host's CombatAuthority, or the client's network sender.</summary>
    public interface IHitClaimSink
    {
        void Submit(in HitClaim claim);
    }
}
