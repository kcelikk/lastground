using LastGround.Data.Presentation;
using Unity.Mathematics;

namespace LastGround.Gameplay.Director
{
    /// <summary>
    /// The ground area a player's top-down camera can show (TDD_01 §9.7), relative to the player, as a trapezoid:
    /// near edge behind the player, wider far edge ahead. The camera has a fixed pitch/yaw (§4), so the host can
    /// compute every player's footprint without asking. Assumes the widest phone aspect (20:9) plus look-ahead margin.
    /// </summary>
    public readonly struct CameraFootprint
    {
        const float WidestAspect = 20f / 9f;

        public readonly float NearZ;
        public readonly float FarZ;
        public readonly float NearHalfWidth;
        public readonly float FarHalfWidth;
        public readonly float Margin;

        public CameraFootprint(CameraProfile camera, float margin)
        {
            float pitch = math.radians(camera.Pitch);
            float halfFov = math.radians(camera.FieldOfView * 0.5f);
            float height = camera.Distance * math.sin(pitch);
            float back = camera.Distance * math.cos(pitch);
            float tanH = WidestAspect * math.tan(halfFov);

            float nearAngle = pitch + halfFov;
            float farAngle = math.max(0.05f, pitch - halfFov);
            NearZ = -back + height / math.tan(nearAngle);
            FarZ = -back + height / math.tan(farAngle);
            // Depth along the optical axis of each edge point sets its half width.
            NearHalfWidth = height / math.sin(nearAngle) * math.cos(halfFov) * tanH;
            FarHalfWidth = height / math.sin(farAngle) * math.cos(halfFov) * tanH;
            Margin = margin;
        }

        /// <summary>True when <paramref name="point"/> could be on the screen of a player standing at <paramref name="player"/>.</summary>
        public bool Contains(float2 player, float2 point)
        {
            float2 d = point - player;
            if (d.y < NearZ - Margin || d.y > FarZ + Margin) return false;
            float t = math.saturate((d.y - NearZ) / math.max(0.01f, FarZ - NearZ));
            return math.abs(d.x) <= math.lerp(NearHalfWidth, FarHalfWidth, t) + Margin;
        }
    }
}
