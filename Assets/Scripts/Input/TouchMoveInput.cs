using LastGround.Core.Input;
using LastGround.Core.Tick;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace LastGround.Input
{
    /// <summary>
    /// Local movement input: floating joystick on the lower-left 75 % of the screen, WASD/arrows as fallback
    /// (editor, desktop, Bluetooth keyboard). Runs in the Input tick phase so the motor sees this frame's value.
    /// M1 has movement only; aim/fire arrive in M4.
    /// </summary>
    public sealed class TouchMoveInput : MonoBehaviour, IPlayerInputSource, ITickable
    {
        const float RadiusDp = 65f;

        [SerializeField] RectTransform _stickBase;
        [SerializeField] RectTransform _stickKnob;

        readonly FloatingJoystick _stick = new FloatingJoystick();
        int _fingerId = -1;
        PlayerInputFrame _frame;

        public PlayerInputFrame Current => _frame;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Dev soak tests: wander in slow circles when there is no touch/keyboard input.</summary>
        public static bool DevWander;
        float _wanderTime;
#endif

        void OnEnable()
        {
            EnhancedTouchSupport.Enable();
            ShowStick(false);
        }

        void OnDisable()
        {
            EnhancedTouchSupport.Disable();
        }

        public void Tick(float dt, uint tick)
        {
            float radius = RadiusDp * DpScale();
            ReadTouches(radius);

            Vector2 move = _stick.Value;
            Keyboard keyboard = Keyboard.current;
            if (move == Vector2.zero && keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) move.x -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move.x += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) move.y -= 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) move.y += 1f;
                move = Vector2.ClampMagnitude(move, 1f);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DevWander && move == Vector2.zero)
            {
                _wanderTime += dt;
                float angle = _wanderTime * 0.35f;
                move = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle * 0.7f)) * 0.8f;
            }
#endif
            _frame.MoveX = move.x;
            _frame.MoveY = move.y;
            UpdateView();
        }

        void ReadTouches(float radius)
        {
            var touches = Touch.activeTouches;
            bool ownerSeen = false;
            for (int i = 0; i < touches.Count; i++)
            {
                Touch touch = touches[i];
                if (touch.finger.index == _fingerId)
                {
                    ownerSeen = true;
                    if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended || touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                    {
                        _stick.End();
                        _fingerId = -1;
                    }
                    else
                    {
                        _stick.Drag(touch.screenPosition, radius);
                    }
                    continue;
                }

                if (_fingerId < 0 && touch.phase == UnityEngine.InputSystem.TouchPhase.Began && InMoveZone(touch.screenPosition))
                {
                    _fingerId = touch.finger.index;
                    _stick.Begin(touch.screenPosition);
                    ownerSeen = true;
                }
            }

            if (!ownerSeen && _fingerId >= 0)
            {
                _stick.End();
                _fingerId = -1;
            }
        }

        static bool InMoveZone(Vector2 position)
        {
            return position.x < Screen.width * 0.5f && position.y < Screen.height * 0.75f;
        }

        static float DpScale()
        {
            float dpi = Screen.dpi;
            return dpi > 0f ? dpi / 160f : 1f;
        }

        void UpdateView()
        {
            ShowStick(_stick.Active);
            if (!_stick.Active || _stickBase == null) return;
            var canvasRect = (RectTransform)_stickBase.parent;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, _stick.Origin, null, out Vector2 local))
                _stickBase.anchoredPosition = local;
            if (_stickKnob != null)
            {
                float scale = canvasRect.rect.width / Mathf.Max(1f, Screen.width);
                _stickKnob.anchoredPosition = _stick.KnobOffset * scale;
            }
        }

        void ShowStick(bool visible)
        {
            if (_stickBase != null && _stickBase.gameObject.activeSelf != visible)
                _stickBase.gameObject.SetActive(visible);
        }
    }
}
