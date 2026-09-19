using LastGround.Data.Objectives;

namespace LastGround.Gameplay.Objectives
{
    /// <summary>Objective lifecycle as shown on the panel.</summary>
    public enum ObjectivePhase : byte
    {
        None = 0,
        /// <summary>Announced: marker and arrow show where, the crate is still on its way (Supply Drop).</summary>
        Announced = 3,
        Active = 1,
        Completed = 2,
        Failed = 4,
    }

    /// <summary>
    /// The current objective as every device sees it (TDD_01 §12.1 EventState + §12.4 ObjectiveState): instance,
    /// kind, region, anchor (world XZ, radius), current/target (counts, or tenths of a second for hold/defend),
    /// phase and the seconds left. The host's ObjectiveSystem writes it; clients receive it.
    /// </summary>
    public sealed class ObjectiveState
    {
        public ushort Instance;
        public ObjectiveKind Kind;
        public int Zone = -1;
        public int Current;
        public int Target;
        public ObjectivePhase Phase;
        public float AnchorX;
        public float AnchorZ;
        /// <summary>Proximity radius in metres; 0 = the whole region.</summary>
        public float Radius;
        /// <summary>Whole seconds left (arrival countdown, time limit, turret); 0 = none.</summary>
        public int SecondsLeft;
        /// <summary>Two-step events: 0 = first step (e.g. kill the cache guard), 1 = second step (open it).</summary>
        public byte Stage;
        public int Version;

        public bool HasAnchor => Radius > 0f;

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

        public void SetEvent(ObjectiveKind kind, float anchorX, float anchorZ, float radius)
        {
            Kind = kind;
            AnchorX = anchorX;
            AnchorZ = anchorZ;
            Radius = radius;
            Stage = 0;
            Version++;
        }

        /// <summary>Moving anchor (the hunted elite); counts as a change only past half a metre.</summary>
        public void MoveAnchor(float x, float z)
        {
            float dx = x - AnchorX, dz = z - AnchorZ;
            if (dx * dx + dz * dz < 0.25f) return;
            AnchorX = x;
            AnchorZ = z;
            Version++;
        }

        public void SetStage(byte stage)
        {
            if (Stage == stage) return;
            Stage = stage;
            Version++;
        }

        /// <summary>Generator sentry (outlives the objective panel): position and whole seconds left, 0 = off.</summary>
        public float TurretX;
        public float TurretZ;
        public int TurretSeconds;

        public void SetTurret(float x, float z, int seconds)
        {
            if (TurretSeconds == seconds && (seconds == 0 || (TurretX == x && TurretZ == z))) return;
            TurretX = x;
            TurretZ = z;
            TurretSeconds = seconds;
            Version++;
        }

        public void SetSeconds(int seconds)
        {
            if (SecondsLeft == seconds) return;
            SecondsLeft = seconds;
            Version++;
        }
    }
}
