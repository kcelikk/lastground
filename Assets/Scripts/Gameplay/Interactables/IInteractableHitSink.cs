namespace LastGround.Gameplay.Interactables
{
    /// <summary>Where the local player's bullet hits on barrels and tanks go (host: InteractableSystem; client: network).</summary>
    public interface IInteractableHitSink
    {
        void HitInteractable(int shooter, int id, float damage);
    }
}
