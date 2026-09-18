using LastGround.Core.Input;
using LastGround.Core.Tick;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace LastGround.Input
{
    /// <summary>
    /// Local twin-stick input (TDD_01 §3.2–3.3): floating move stick on the lower-left, floating aim stick on the
    /// lower-right. Aim stick past 0.25 aims, past 0.55 fires; after release the last aim is kept for 0.15 s
    /// (flick shots). Keyboard/mouse fallback for editor and desktop: WASD/arrows move, IJKL aim + fire, or hold the
    /// left mouse button to fire towards the cursor. Runs in the Input tick phase.
    /// </summary>
    public sealed class TouchTwinStickInput : MonoBehaviour, IPlayerInputSource, ITickable
    {
        const float RadiusDp = 65f;
        const float AimThreshold = 0.25f;
        const float FireThreshold = 0.55f;
        const float FlickHold = 0.15f;
        const float ZoneTop = 0.75f;

        [SerializeField] RectTransform _moveBase;
        [SerializeField] RectTransform _moveKnob;
        [SerializeField] RectTransform _aimBase;
        [SerializeField] RectTransform _aimKnob;
        [SerializeField] UnityEngine.UI.Image _aimKnobImage;

        readonly FloatingJoystick _move = new FloatingJoystick();
        readonly FloatingJoystick _aim = new FloatingJoystick();
        int _moveFinger = -1;
        int _aimFinger = -1;
        float _flickTimer;
        Vector2 _lastAim;
        PlayerInputFrame _frame;

        public PlayerInputFrame Current => _frame;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Dev soak tests: wander in slow circles when there is no touch/keyboard input.</summary>
        public static bool DevWander;

        /// <summary>Dev soak tests: when non-zero, the wander bot walks this way instead (e.g. to a downed teammate).</summary>
        public static Vector2 DevSeek;
        float _wanderTime;
#endif

        void OnEnable()
        {
            EnhancedTouchSupport.Enable();
            Show(_moveBase, false);
            Show(_aimBase, false);
        }

        void OnDisable()
        {
            EnhancedTouchSupport.Disable();
        }

        public void Tick(float dt, uint tick)
        {
            float radius = RadiusDp * DpScale();
            ReadTouches(radius);

            Vector2 move = _move.Value;
            Vector2 aim = _aim.Value;
            bool fire = aim.sqrMagnitude > FireThreshold * FireThreshold;
            ReadKeyboardAndMouse(ref move, ref aim, ref fire);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DevWander && move == Vector2.zero && DevSeek != Vector2.zero)
            {
                move = DevSeek;
            }
            else if (DevWander && move == Vector2.zero)
            {
                _wanderTime += dt;
                float angle = _wanderTime * 0.35f;
                move = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle * 0.7f)) * 0.8f;
            }
#endif
            bool aiming = aim.sqrMagnitude > AimThreshold * AimThreshold;
            if (aiming)
            {
                _lastAim = aim.normalized;
                _flickTimer = FlickHold;
            }
            else if (_flickTimer > 0f)
            {
                _flickTimer -= dt;
                aiming = _flickTimer > 0f;
            }

            _frame.MoveX = move.x;
            _frame.MoveY = move.y;
            _frame.AimActive = aiming;
            _frame.AimX = aiming ? _lastAim.x : 0f;
            _frame.AimY = aiming ? _lastAim.y : 0f;
            _frame.FireHeld = aiming && fire;
            UpdateView(_move, _moveBase, _moveKnob);
            UpdateView(_aim, _aimBase, _aimKnob);
            if (_aimKnobImage != null) _aimKnobImage.color = fire ? new Color(1f, 0.35f, 0.25f, 0.8f) : new Color(1f, 1f, 1f, 0.5f);
        }

        void ReadKeyboardAndMouse(ref Vector2 move, ref Vector2 aim, ref bool fire)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (move == Vector2.zero)
                {
                    if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) move.x -= 1f;
                    if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move.x += 1f;
                    if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) move.y -= 1f;
                    if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) move.y += 1f;
                    move = Vector2.ClampMagnitude(move, 1f);
                }
                if (aim == Vector2.zero)
                {
                    Vector2 keys = Vector2.zero;
                    if (keyboard.jKey.isPressed) keys.x -= 1f;
                    if (keyboard.lKey.isPressed) keys.x += 1f;
                    if (keyboard.kKey.isPressed) keys.y -= 1f;
                    if (keyboard.iKey.isPressed) keys.y += 1f;
                    if (keys != Vector2.zero)
                    {
                        aim = keys.normalized;
                        fire = true;
                    }
                }
            }

            Mouse mouse = Mouse.current;
            if (aim == Vector2.zero && mouse != null && mouse.leftButton.isPressed && Touch.activeTouches.Count == 0)
            {
                // The camera keeps the player near the screen centre with yaw 0: screen up = world forward.
                Vector2 fromCentre = mouse.position.ReadValue() - new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                if (fromCentre.sqrMagnitude > 1f)
                {
                    aim = fromCentre.normalized;
                    fire = true;
                }
            }
        }

        void ReadTouches(float radius)
        {
            var touches = Touch.activeTouches;
            bool moveSeen = false, aimSeen = false;
            for (int i = 0; i < touches.Count; i++)
            {
                Touch touch = touches[i];
                int finger = touch.finger.index;
                bool ended = touch.phase == UnityEngine.InputSystem.TouchPhase.Ended || touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled;
                if (finger == _moveFinger)
                {
                    moveSeen = true;
                    if (ended) { _move.End(); _moveFinger = -1; }
                    else _move.Drag(touch.screenPosition, radius);
                    continue;
                }
                if (finger == _aimFinger)
                {
                    aimSeen = true;
                    if (ended) { _aim.End(); _aimFinger = -1; }
                    else _aim.Drag(touch.screenPosition, radius);
                    continue;
                }
                if (touch.phase != UnityEngine.InputSystem.TouchPhase.Began) continue;

                Vector2 p = touch.screenPosition;
                if (p.y > Screen.height * ZoneTop) continue;
                if (p.x < Screen.width * 0.5f)
                {
                    if (_moveFinger >= 0) continue;
                    _moveFinger = finger;
                    _move.Begin(p);
                    moveSeen = true;
                }
                else
                {
                    if (_aimFinger >= 0) continue;
                    _aimFinger = finger;
                    _aim.Begin(p);
                    aimSeen = true;
                }
            }

            if (!moveSeen && _moveFinger >= 0) { _move.End(); _moveFinger = -1; }
            if (!aimSeen && _aimFinger >= 0) { _aim.End(); _aimFinger = -1; }
        }

        static float DpScale()
        {
            float dpi = Screen.dpi;
            return dpi > 0f ? dpi / 160f : 1f;
        }

        static void UpdateView(FloatingJoystick stick, RectTransform stickBase, RectTransform knob)
        {
            Show(stickBase, stick.Active);
            if (!stick.Active || stickBase == null) return;
            var canvasRect = (RectTransform)stickBase.parent;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, stick.Origin, null, out Vector2 local))
                stickBase.anchoredPosition = local;
            if (knob != null)
            {
                float scale = canvasRect.rect.width / Mathf.Max(1f, Screen.width);
                knob.anchoredPosition = stick.KnobOffset * scale;
            }
        }

        static void Show(RectTransform rect, bool visible)
        {
            if (rect != null && rect.gameObject.activeSelf != visible) rect.gameObject.SetActive(visible);
        }
    }
}
