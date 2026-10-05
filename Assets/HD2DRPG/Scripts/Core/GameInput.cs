using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HD2DRPG
{
    /// <summary>
    /// Thin input facade that works with either the new Input System package or the
    /// legacy Input Manager, depending on the project's "Active Input Handling" setting.
    ///
    /// Keyboard:  Arrows / WASD = move & navigate, Z / Enter / Space = confirm,
    ///            X / Backspace / Esc = cancel, C / Tab / M = open menu,
    ///            Q / E (or PageUp/PageDown) = boost down / up in battle & switch member in menus.
    /// Gamepad:   Stick / D-pad, South = confirm, East = cancel, North/Start = menu, shoulders = boost.
    /// </summary>
    public static class GameInput
    {
        const float RepeatDelay = 0.32f;
        const float RepeatRate = 0.085f;

        static int lastFrame = -1;
        static Vector2Int navHeld;
        static float navTimer;
        static Vector2Int navPulse;
        static int consumedFrame = -1;

        /// <summary>Swallow all button presses for the rest of this frame (prevents one key press
        /// from being handled by two screens, e.g. opening and instantly closing a menu).</summary>
        public static void Consume() { consumedFrame = Time.frameCount; }
        static bool Blocked => consumedFrame == Time.frameCount;

        public static Vector2 Move
        {
            get
            {
                Vector2 v = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
                var kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb[Key.LeftArrow].isPressed || kb[Key.A].isPressed) v.x -= 1;
                    if (kb[Key.RightArrow].isPressed || kb[Key.D].isPressed) v.x += 1;
                    if (kb[Key.UpArrow].isPressed || kb[Key.W].isPressed) v.y += 1;
                    if (kb[Key.DownArrow].isPressed || kb[Key.S].isPressed) v.y -= 1;
                }
                var gp = Gamepad.current;
                if (gp != null)
                {
                    Vector2 s = gp.leftStick.ReadValue();
                    if (s.sqrMagnitude > 0.08f) v += s;
                    if (gp.dpad.left.isPressed) v.x -= 1;
                    if (gp.dpad.right.isPressed) v.x += 1;
                    if (gp.dpad.up.isPressed) v.y += 1;
                    if (gp.dpad.down.isPressed) v.y -= 1;
                }
#elif ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) v.x -= 1;
                if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) v.x += 1;
                if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) v.y += 1;
                if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) v.y -= 1;
#endif
                return Vector2.ClampMagnitude(v, 1f);
            }
        }

        /// <summary>Discrete navigation pulse with key-repeat, for menus.</summary>
        public static Vector2Int Nav
        {
            get
            {
                UpdateNav();
                return navPulse;
            }
        }

        public static bool Up => Nav.y > 0;
        public static bool Down => Nav.y < 0;
        public static bool Left => Nav.x < 0;
        public static bool Right => Nav.x > 0;

        static void UpdateNav()
        {
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            Vector2 m = Move;
            var dir = Vector2Int.zero;
            if (Mathf.Abs(m.y) >= Mathf.Abs(m.x) && Mathf.Abs(m.y) > 0.5f) dir.y = m.y > 0 ? 1 : -1;
            else if (Mathf.Abs(m.x) > 0.5f) dir.x = m.x > 0 ? 1 : -1;

            navPulse = Vector2Int.zero;
            if (Blocked) { navHeld = dir; navTimer = RepeatDelay; return; }
            if (dir == Vector2Int.zero) { navHeld = dir; return; }
            if (dir != navHeld)
            {
                navHeld = dir;
                navPulse = dir;
                navTimer = RepeatDelay;
                return;
            }
            navTimer -= Time.unscaledDeltaTime;
            if (navTimer <= 0f)
            {
                navPulse = dir;
                navTimer = RepeatRate;
            }
        }

        public static bool Confirm
        {
            get
            {
                if (Blocked) return false;
#if ENABLE_INPUT_SYSTEM
                var kb = Keyboard.current;
                var gp = Gamepad.current;
                return (kb != null && (kb[Key.Z].wasPressedThisFrame || kb[Key.Enter].wasPressedThisFrame ||
                                       kb[Key.Space].wasPressedThisFrame || kb[Key.NumpadEnter].wasPressedThisFrame))
                       || (gp != null && gp.buttonSouth.wasPressedThisFrame);
#elif ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Return) ||
                       Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.KeypadEnter);
#else
                return false;
#endif
            }
        }

        public static bool Cancel
        {
            get
            {
                if (Blocked) return false;
#if ENABLE_INPUT_SYSTEM
                var kb = Keyboard.current;
                var gp = Gamepad.current;
                return (kb != null && (kb[Key.X].wasPressedThisFrame || kb[Key.Backspace].wasPressedThisFrame ||
                                       kb[Key.Escape].wasPressedThisFrame))
                       || (gp != null && gp.buttonEast.wasPressedThisFrame);
#elif ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.Backspace) ||
                       Input.GetKeyDown(KeyCode.Escape);
#else
                return false;
#endif
            }
        }

        public static bool Menu
        {
            get
            {
                if (Blocked) return false;
#if ENABLE_INPUT_SYSTEM
                var kb = Keyboard.current;
                var gp = Gamepad.current;
                return (kb != null && (kb[Key.C].wasPressedThisFrame || kb[Key.Tab].wasPressedThisFrame ||
                                       kb[Key.M].wasPressedThisFrame || kb[Key.Escape].wasPressedThisFrame))
                       || (gp != null && (gp.buttonNorth.wasPressedThisFrame || gp.startButton.wasPressedThisFrame));
#elif ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.Tab) ||
                       Input.GetKeyDown(KeyCode.M) || Input.GetKeyDown(KeyCode.Escape);
#else
                return false;
#endif
            }
        }

        /// <summary>Boost up (battle) / next member (menus).</summary>
        public static bool PageRight
        {
            get
            {
                if (Blocked) return false;
#if ENABLE_INPUT_SYSTEM
                var kb = Keyboard.current;
                var gp = Gamepad.current;
                return (kb != null && kb[Key.E].wasPressedThisFrame) ||
                       (gp != null && gp.rightShoulder.wasPressedThisFrame);
#elif ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.PageDown);
#else
                return false;
#endif
            }
        }

        /// <summary>Boost down (battle) / previous member (menus).</summary>
        public static bool PageLeft
        {
            get
            {
                if (Blocked) return false;
#if ENABLE_INPUT_SYSTEM
                var kb = Keyboard.current;
                var gp = Gamepad.current;
                return (kb != null && kb[Key.Q].wasPressedThisFrame) ||
                       (gp != null && gp.leftShoulder.wasPressedThisFrame);
#elif ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.PageUp);
#else
                return false;
#endif
            }
        }

        /// <summary>Held run modifier (Shift / gamepad West).</summary>
        public static bool Run
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var kb = Keyboard.current;
                var gp = Gamepad.current;
                return (kb != null && (kb[Key.LeftShift].isPressed || kb[Key.RightShift].isPressed)) ||
                       (gp != null && gp.buttonWest.isPressed);
#elif ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#else
                return false;
#endif
            }
        }
    }
}
