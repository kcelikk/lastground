namespace LastGround.Core.Pooling
{
    /// <summary>Component that is reused through a <see cref="ComponentPool{T}"/> instead of Instantiate/Destroy.</summary>
    public interface IPoolable
    {
        void OnSpawned();
        void OnDespawned();
    }
}
