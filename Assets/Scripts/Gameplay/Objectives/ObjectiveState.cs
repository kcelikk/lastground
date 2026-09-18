namespace LastGround.Gameplay.Objectives
{
    /// <summary>Objective lifecycle as shown on the panel.</summary>
    public enum ObjectivePhase : byte
    {
        None = 0,
        Active = 1,
        Completed = 2,
    }

    /// <summary>
    /// The current objective as every device sees it (TDD_01 §12.4 ObjectiveState: instance, zone, current/target,
    /// phase). The host's ObjectiveSystem writes it; clients receive it.
    /// </summary>
    public sealed class ObjectiveState
    {
        public ushort Instance;
        public int Zone = -1;
        public int Current;
        public int Target;
        public ObjectivePhase Phase;
        public int Version;

        public void Set(ushort instance, int zone, int current, int target, ObjectivePhase phase)
        {
            if (Instance == instance && Zone == zone && Current == current && Target == target && Phase == phase) return;
            Instance = instance;
            Zone = zone;
            Current = current;
            Target = target;
            Phase = phase;
            Version++;
        }
    }
}
