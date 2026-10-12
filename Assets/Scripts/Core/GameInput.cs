using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ProjectJuice
{
    /// <summary>
    /// One tiny place that reads input, so the rest of the code works with the old Input Manager
    /// and with the new Input System package (Unity 6 default).
    /// </summary>
    public static class GameInput
    {
        public static Vector2 PointerPosition
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
                return Input.mousePosition;
#endif
            }
        }

        public static bool LeftDown
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
                return Input.GetMouseButtonDown(0);
#endif
            }
        }

        public static bool LeftUp
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;
#else
                return Input.GetMouseButtonUp(0);
#endif
            }
        }

        public static bool RightDown
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
#else
                return Input.GetMouseButtonDown(1);
#endif
            }
        }

        public static bool SwitchDown    { get { return KeyDown(0); } }
        public static bool EscapeDown    { get { return KeyDown(1); } }
        public static bool RespawnDown   { get { return KeyDown(2); } }
        public static bool ResetAreaDown { get { return KeyDown(3); } }
        public static bool PrevAreaDown  { get { return KeyDown(4); } }
        public static bool NextAreaDown  { get { return KeyDown(5); } }

        public static bool SnapHeld
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
#else
                return Input.GetKey(KeyCode.LeftShift);
#endif
            }
        }

        /// <summary>index 0..9 maps to the keys 1,2,...,9,0.</summary>
        public static bool DigitDown(int index)
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current == null) return false;
            Key[] keys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0 };
            return index >= 0 && index < keys.Length && Keyboard.current[keys[index]].wasPressedThisFrame;
#else
            KeyCode[] keys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0 };
            return index >= 0 && index < keys.Length && Input.GetKeyDown(keys[index]);
#endif
        }

        // 0 Space, 1 Escape, 2 R, 3 T, 4 [, 5 ]
        static bool KeyDown(int which)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard k = Keyboard.current;
            if (k == null) return false;
            switch (which)
            {
                case 0: return k.spaceKey.wasPressedThisFrame;
                case 1: return k.escapeKey.wasPressedThisFrame;
                case 2: return k.rKey.wasPressedThisFrame;
                case 3: return k.tKey.wasPressedThisFrame;
                case 4: return k.leftBracketKey.wasPressedThisFrame;
                case 5: return k.rightBracketKey.wasPressedThisFrame;
            }
            return false;
#else
            switch (which)
            {
                case 0: return Input.GetKeyDown(KeyCode.Space);
                case 1: return Input.GetKeyDown(KeyCode.Escape);
                case 2: return Input.GetKeyDown(KeyCode.R);
                case 3: return Input.GetKeyDown(KeyCode.T);
                case 4: return Input.GetKeyDown(KeyCode.LeftBracket);
                case 5: return Input.GetKeyDown(KeyCode.RightBracket);
            }
            return false;
#endif
        }
    }
}
