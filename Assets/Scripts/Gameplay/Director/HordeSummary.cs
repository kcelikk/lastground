namespace LastGround.Gameplay.Director
{
    /// <summary>
    /// Far horde groups (TDD_02 §17.7 <c>HordeGroupSummary</c>, 1 Hz): position and size of each virtual group, for the
    /// mini-map direction ring and distant horde ambience. The host's director fills it; clients receive it.
    /// </summary>
    public sealed class HordeSummary
    {
        public const int MaxGroups = 8;

        public readonly float[] X = new float[MaxGroups];
        public readonly float[] Z = new float[MaxGroups];
        public readonly int[] Size = new int[MaxGroups];
        public int Count;
        public int Version;

        public void Clear() => Count = 0;

        public void Add(float x, float z, int size)
        {
            if (Count >= MaxGroups) return;
            X[Count] = x;
            Z[Count] = z;
            Size[Count] = size;
            Count++;
        }

        /// <summary>Call after rewriting the groups.</summary>
        public void Commit() => Version++;
    }
}
