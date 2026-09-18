using UnityEngine;

namespace LastGround.Data.Presentation
{
    /// <summary>Top-down camera tuning (TDD_01 §4).</summary>
    [CreateAssetMenu(menuName = "LastGround/Camera/Profile")]
    public sealed class CameraProfile : ScriptableObject
    {
        public float Pitch = 55f;
        public float FieldOfView = 35f;
        public float Distance = 22f;
        public float FollowSmoothTime = 0.12f;

        [Header("Look-ahead")]
        public float AimLookAhead = 2.5f;
        public float MoveLookAhead = 1.5f;
        public float LookAheadSmoothTime = 0.35f;

        [Header("Shake (trauma model)")]
        public float MaxShakeOffset = 0.3f;
        public float MaxShakeAngle = 1.5f;
        public float ShakeFrequency = 18f;
        /// <summary>Trauma lost per second.</summary>
        public float TraumaDecay = 1.6f;
        public float HurtTrauma = 0.45f;
    }
}
