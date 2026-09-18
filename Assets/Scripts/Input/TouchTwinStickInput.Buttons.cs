using UnityEngine;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace LastGround.Input
{
    /// <summary>
    /// Action buttons around the aim stick (TDD_01 §3.5), hit-tested by the input itself: weapon swap is a tap; the
    /// grenade throws along the aim on a tap, or is aimed by dragging from the button and thrown on release.
    /// </summary>
    public sealed partial class TouchTwinStickInput
    {
        [SerializeField] RectTransform _switchButton;
        [SerializeField] RectTransform _grenadeButton;

        /// <summary>Grenade tap throws this far along the aim; a full drag throws this far (world metres).</summary>
        public float GrenadeTapDistance { get; set; } = 8f;
        public float GrenadeMaxDistance { get; set; } = 12f;

        /// <summary>UI buttons (e.g. "take weapon"): a touch that starts on one never starts a stick.</summary>
        public readonly System.Collections.Generic.List<RectTransform> Blockers = new System.Collections.Generic.List<RectTransform>();

        /// <summary>Grenade being aimed by dragging: world offset from the player (presentation shows a landing ring).</summary>
        public bool GrenadeAiming => _grenadeFinger >= 0 && _grenadeDragged;
        public Vector2 GrenadeAimOffset => _grenadeOffset;

        /// <summary>UI areas where a new touch belongs to the UI, not to a stick (e.g. the level-up panel).</summary>
        public System.Func<RectTransform> Blocker { get; set; }

        int _grenadeFinger = -1;
        Vector2 _grenadeStart;
        bool _grenadeDragged;
        Vector2 _grenadeOffset;
        bool _switchPressed;
        bool _throwPressed;
        Vector2 _throwOffset;

        void DragGrenade(Vector2 position, float radius, bool released)
        {
            Vector2 drag = (position - _grenadeStart) / Mathf.Max(1f, radius);
            if (drag.sqrMagnitude > 0.04f) _grenadeDragged = true;
            _grenadeOffset = Vector2.ClampMagnitude(drag, 1f) * GrenadeMaxDistance;
            if (!released) return;
            _grenadeFinger = -1;
            if (_grenadeDragged)
            {
                _throwPressed = true;
                _throwOffset = _grenadeOffset;
            }
            else
            {
                ThrowAlongAim();
            }
            _grenadeDragged = false;
        }

        /// <summary>Tap: throw along the current (or last) aim direction.</summary>
        void ThrowAlongAim()
        {
            _throwPressed = true;
            _throwOffset = _lastAim * GrenadeTapDistance;
        }

        bool IsBlocked(Vector2 p)
        {
            for (int i = 0; i < Blockers.Count; i++)
                if (Blockers[i] != null && Blockers[i].gameObject.activeInHierarchy && Contains(Blockers[i], p)) return true;
            return false;
        }

        static bool Contains(RectTransform rect, Vector2 p) =>
            rect != null && rect.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(rect, p, null);

        static bool FingerActive(UnityEngine.InputSystem.Utilities.ReadOnlyArray<Touch> touches, int finger)
        {
            for (int i = 0; i < touches.Count; i++) if (touches[i].finger.index == finger) return true;
            return false;
        }
    }
}
