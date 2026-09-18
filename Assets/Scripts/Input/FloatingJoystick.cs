using UnityEngine;

namespace LastGround.Input
{
    /// <summary>
    /// Floating stick logic (TDD_01 §3.2): the origin is where the finger lands; the value is the offset divided by
    /// the radius, with a dead zone. Pure math, no UI or device code.
    /// </summary>
    public sealed class FloatingJoystick
    {
        readonly float _deadZone;
        Vector2 _origin;

        public FloatingJoystick(float deadZone = 0.12f)
        {
            _deadZone = deadZone;
        }

        public bool Active { get; private set; }
        public Vector2 Origin => _origin;
        public Vector2 Value { get; private set; }

        /// <summary>Knob offset from the origin in pixels, clamped to the radius (for the view).</summary>
        public Vector2 KnobOffset { get; private set; }

        public void Begin(Vector2 position)
        {
            Active = true;
            _origin = position;
            Value = Vector2.zero;
            KnobOffset = Vector2.zero;
        }

        public void Drag(Vector2 position, float radius)
        {
            if (!Active || radius <= 0f) return;
            Vector2 offset = Vector2.ClampMagnitude(position - _origin, radius);
            KnobOffset = offset;
            Vector2 value = offset / radius;
            float magnitude = value.magnitude;
            Value = magnitude < _deadZone ? Vector2.zero : value * ((magnitude - _deadZone) / (1f - _deadZone) / magnitude);
        }

        public void End()
        {
            Active = false;
            Value = Vector2.zero;
            KnobOffset = Vector2.zero;
        }
    }
}
