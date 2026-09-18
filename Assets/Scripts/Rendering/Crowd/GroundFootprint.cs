using UnityEngine;

namespace LastGround.Rendering.Crowd
{
    /// <summary>
    /// The camera's view projected onto the ground plane (y = 0) as a convex quad, expanded by a margin.
    /// With a fixed top-down camera this is exact and cheaper than frustum tests (TDD_02 §21.3 culling).
    /// </summary>
    public sealed class GroundFootprint
    {
        const float MaxRayDistance = 120f;

        readonly Vector2[] _corners = new Vector2[4];
        static readonly Vector2[] Viewport = { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };

        public Vector2 Focus { get; private set; }

        public void Update(Camera camera, float margin)
        {
            Vector2 centre = Vector2.zero;
            for (int i = 0; i < 4; i++)
            {
                _corners[i] = Project(camera.ViewportPointToRay(new Vector3(Viewport[i].x, Viewport[i].y, 0f)));
                centre += _corners[i];
            }
            centre *= 0.25f;
            for (int i = 0; i < 4; i++)
            {
                Vector2 outward = _corners[i] - centre;
                float length = outward.magnitude;
                if (length > 1e-3f) _corners[i] += outward / length * margin;
            }
            Focus = Project(camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)));
        }

        public bool Contains(float x, float z)
        {
            bool? sign = null;
            for (int i = 0; i < 4; i++)
            {
                Vector2 a = _corners[i];
                Vector2 b = _corners[(i + 1) & 3];
                float cross = (b.x - a.x) * (z - a.y) - (b.y - a.y) * (x - a.x);
                bool positive = cross >= 0f;
                if (sign == null) sign = positive;
                else if (sign.Value != positive) return false;
            }
            return true;
        }

        static Vector2 Project(Ray ray)
        {
            float t = ray.direction.y < -1e-4f ? -ray.origin.y / ray.direction.y : MaxRayDistance;
            t = Mathf.Min(t, MaxRayDistance);
            Vector3 p = ray.origin + ray.direction * t;
            return new Vector2(p.x, p.z);
        }
    }
}
