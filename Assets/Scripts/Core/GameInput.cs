using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CastleOfTheD20.Core
{
    /// <summary>
    /// Resilient hybrid input bridge combining Unity's New Input System (UnityEngine.InputSystem)
    /// with robust fallbacks to Legacy Input (UnityEngine.Input).
    /// Provides zero-allocation, WebGL-safe polling with null-checks and multi-backend coverage
    /// ensuring keyboard, mouse, and gamepad inputs operate seamlessly across Editor, desktop, and WebGL.
    /// </summary>
    public static class GameInput
    {
        #region Mouse Queries

        /// <summary>
        /// True during the frame the user pressed the primary mouse button (Left Click).
        /// </summary>
        public static bool GetLeftMouseButtonDown()
        {
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                return true;
            }

            try
            {
                return Input.GetMouseButtonDown(0);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// True while the primary mouse button (Left Click) is held down.
        /// </summary>
        public static bool GetLeftMouseButton()
        {
            if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            {
                return true;
            }

            try
            {
                return Input.GetMouseButton(0);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// True during the frame the user released the primary mouse button (Left Click).
        /// </summary>
        public static bool GetLeftMouseButtonUp()
        {
            if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                return true;
            }

            try
            {
                return Input.GetMouseButtonUp(0);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// True during the frame the user pressed the secondary mouse button (Right Click).
        /// </summary>
        public static bool GetRightMouseButtonDown()
        {
            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            {
                return true;
            }

            try
            {
                return Input.GetMouseButtonDown(1);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// True while the secondary mouse button (Right Click) is held down.
        /// </summary>
        public static bool GetRightMouseButton()
        {
            if (Mouse.current != null && Mouse.current.rightButton.isPressed)
            {
                return true;
            }

            try
            {
                return Input.GetMouseButton(1);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// True during the frame the user released the secondary mouse button (Right Click).
        /// </summary>
        public static bool GetRightMouseButtonUp()
        {
            if (Mouse.current != null && Mouse.current.rightButton.wasReleasedThisFrame)
            {
                return true;
            }

            try
            {
                return Input.GetMouseButtonUp(1);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// True during the frame the user pressed the middle mouse button.
        /// </summary>
        public static bool GetMiddleMouseButtonDown()
        {
            if (Mouse.current != null && Mouse.current.middleButton.wasPressedThisFrame)
            {
                return true;
            }

            try
            {
                return Input.GetMouseButtonDown(2);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// True while the middle mouse button is held down.
        /// </summary>
        public static bool GetMiddleMouseButton()
        {
            if (Mouse.current != null && Mouse.current.middleButton.isPressed)
            {
                return true;
            }

            try
            {
                return Input.GetMouseButton(2);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Returns the mouse movement delta vector (X = Horizontal, Y = Vertical) since last frame.
        /// </summary>
        public static Vector2 GetMouseDelta()
        {
            if (Mouse.current != null)
            {
                Vector2 delta = Mouse.current.delta.ReadValue();
                if (delta.sqrMagnitude > 0.0001f)
                {
                    return delta;
                }
            }

            try
            {
                return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
            }
            catch
            {
                return Vector2.zero;
            }
        }

        /// <summary>
        /// Current screen coordinates of the mouse cursor in pixels.
        /// </summary>
        public static Vector2 GetMousePosition()
        {
            if (Mouse.current != null)
            {
                Vector2 pos = Mouse.current.position.ReadValue();
                if (pos.sqrMagnitude > 0.001f)
                {
                    return pos;
                }
            }

            try
            {
                return Input.mousePosition;
            }
            catch
            {
                return Vector2.zero;
            }
        }

        /// <summary>
        /// Mouse scroll wheel delta vector since last frame.
        /// </summary>
        public static Vector2 GetMouseScrollDelta()
        {
            if (Mouse.current != null)
            {
                Vector2 scroll = Mouse.current.scroll.ReadValue();
                if (scroll.sqrMagnitude > 0.0001f)
                {
                    return scroll;
                }
            }

            try
            {
                return Input.mouseScrollDelta;
            }
            catch
            {
                return Vector2.zero;
            }
        }

        #endregion

        #region Input Suspension & State Controls

        /// <summary>
        /// When false, exploration movement inputs return Vector2.zero to suspend world navigation
        /// during modal windows, milestone level-ups, or scripted events.
        /// </summary>
        public static bool IsExplorationInputEnabled { get; set; } = true;

        public static void SetExplorationInputEnabled(bool enabled)
        {
            IsExplorationInputEnabled = enabled;
        }

        #endregion

        #region Keyboard & Locomotion Queries

        /// <summary>
        /// Returns a normalized 2D movement vector (X = Horizontal [-1..1], Y = Vertical [-1..1])
        /// polled from WASD, arrow keys, or gamepad thumbsticks using New Input System with Legacy fallback.
        /// </summary>
        public static Vector2 GetMovementVector()
        {
            if (!IsExplorationInputEnabled)
            {
                return Vector2.zero;
            }

            float horizontal = 0f;
            float vertical = 0f;

            // 1. New Input System - Keyboard
            if (Keyboard.current != null)
            {
                var k = Keyboard.current;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) horizontal += 1f;
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) horizontal -= 1f;
                if (k.wKey.isPressed || k.upArrowKey.isPressed) vertical += 1f;
                if (k.sKey.isPressed || k.downArrowKey.isPressed) vertical -= 1f;
            }

            // 2. New Input System - Gamepad
            if (Gamepad.current != null)
            {
                Vector2 stick = Gamepad.current.leftStick.ReadValue();
                if (stick.sqrMagnitude > 0.04f)
                {
                    horizontal += stick.x;
                    vertical += stick.y;
                }
                if (Gamepad.current.dpad.right.isPressed) horizontal += 1f;
                if (Gamepad.current.dpad.left.isPressed) horizontal -= 1f;
                if (Gamepad.current.dpad.up.isPressed) vertical += 1f;
                if (Gamepad.current.dpad.down.isPressed) vertical -= 1f;
            }

            // 3. Legacy Input Manager Fallback (Active when Keyboard.current is dormant, uninitialized, or in Both mode)
            if (Mathf.Approximately(horizontal, 0f) && Mathf.Approximately(vertical, 0f))
            {
                try
                {
                    if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) horizontal += 1f;
                    if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) horizontal -= 1f;
                    if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) vertical += 1f;
                    if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) vertical -= 1f;

                    if (Mathf.Approximately(horizontal, 0f))
                    {
                        horizontal = Input.GetAxisRaw("Horizontal");
                    }
                    if (Mathf.Approximately(vertical, 0f))
                    {
                        vertical = Input.GetAxisRaw("Vertical");
                    }
                }
                catch
                {
                    // Ignored if legacy input is unavailable
                }
            }

            return new Vector2(Mathf.Clamp(horizontal, -1f, 1f), Mathf.Clamp(vertical, -1f, 1f));
        }

        /// <summary>
        /// True during the frame the Quick Potion hotkey [Q] was pressed.
        /// </summary>
        public static bool IsQuickPotionHotkeyPressed()
        {
            if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
            {
                return true;
            }

            try
            {
                return Input.GetKeyDown(KeyCode.Q);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// True during the frame the Map toggle hotkey [M] was pressed.
        /// </summary>
        public static bool IsMapHotkeyPressed()
        {
            if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
            {
                return true;
            }

            try
            {
                return Input.GetKeyDown(KeyCode.M);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// True during the frame any user input (key press or mouse click) occurred.
        /// Used for audio autoplay unmuting and wake-from-idle checks.
        /// </summary>
        public static bool IsAnyInputDetected()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            {
                return true;
            }
            if (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame || Mouse.current.middleButton.wasPressedThisFrame))
            {
                return true;
            }
            if (Gamepad.current != null && (Gamepad.current.buttonSouth.wasPressedThisFrame || Gamepad.current.startButton.wasPressedThisFrame))
            {
                return true;
            }

            try
            {
                return Input.anyKeyDown;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Cross-compatible query for standard KeyCodes using New Input System with Legacy fallback.
        /// </summary>
        public static bool GetKeyDown(KeyCode key)
        {
            if (Keyboard.current != null)
            {
                var k = Keyboard.current;
                bool pressed = false;
                switch (key)
                {
                    case KeyCode.Q: pressed = k.qKey.wasPressedThisFrame; break;
                    case KeyCode.E: pressed = k.eKey.wasPressedThisFrame; break;
                    case KeyCode.W: pressed = k.wKey.wasPressedThisFrame; break;
                    case KeyCode.A: pressed = k.aKey.wasPressedThisFrame; break;
                    case KeyCode.S: pressed = k.sKey.wasPressedThisFrame; break;
                    case KeyCode.D: pressed = k.dKey.wasPressedThisFrame; break;
                    case KeyCode.R: pressed = k.rKey.wasPressedThisFrame; break;
                    case KeyCode.F: pressed = k.fKey.wasPressedThisFrame; break;
                    case KeyCode.C: pressed = k.cKey.wasPressedThisFrame; break;
                    case KeyCode.V: pressed = k.vKey.wasPressedThisFrame; break;
                    case KeyCode.X: pressed = k.xKey.wasPressedThisFrame; break;
                    case KeyCode.Z: pressed = k.zKey.wasPressedThisFrame; break;
                    case KeyCode.I: pressed = k.iKey.wasPressedThisFrame; break;
                    case KeyCode.M: pressed = k.mKey.wasPressedThisFrame; break;
                    case KeyCode.P: pressed = k.pKey.wasPressedThisFrame; break;
                    case KeyCode.Space: pressed = k.spaceKey.wasPressedThisFrame; break;
                    case KeyCode.Escape: pressed = k.escapeKey.wasPressedThisFrame; break;
                    case KeyCode.Tab: pressed = k.tabKey.wasPressedThisFrame; break;
                    case KeyCode.Return: pressed = k.enterKey.wasPressedThisFrame; break;
                    case KeyCode.Alpha1: pressed = k.digit1Key.wasPressedThisFrame; break;
                    case KeyCode.Alpha2: pressed = k.digit2Key.wasPressedThisFrame; break;
                    case KeyCode.Alpha3: pressed = k.digit3Key.wasPressedThisFrame; break;
                    case KeyCode.Alpha4: pressed = k.digit4Key.wasPressedThisFrame; break;
                    case KeyCode.Alpha5: pressed = k.digit5Key.wasPressedThisFrame; break;
                    case KeyCode.Alpha6: pressed = k.digit6Key.wasPressedThisFrame; break;
                    case KeyCode.Alpha7: pressed = k.digit7Key.wasPressedThisFrame; break;
                    case KeyCode.Alpha8: pressed = k.digit8Key.wasPressedThisFrame; break;
                    case KeyCode.Alpha9: pressed = k.digit9Key.wasPressedThisFrame; break;
                    case KeyCode.Alpha0: pressed = k.digit0Key.wasPressedThisFrame; break;
                    case KeyCode.LeftShift: pressed = k.leftShiftKey.wasPressedThisFrame; break;
                    case KeyCode.RightShift: pressed = k.rightShiftKey.wasPressedThisFrame; break;
                    case KeyCode.LeftControl: pressed = k.leftCtrlKey.wasPressedThisFrame; break;
                    case KeyCode.RightControl: pressed = k.rightCtrlKey.wasPressedThisFrame; break;
                    case KeyCode.LeftAlt: pressed = k.leftAltKey.wasPressedThisFrame; break;
                    case KeyCode.RightAlt: pressed = k.rightAltKey.wasPressedThisFrame; break;
                    case KeyCode.UpArrow: pressed = k.upArrowKey.wasPressedThisFrame; break;
                    case KeyCode.DownArrow: pressed = k.downArrowKey.wasPressedThisFrame; break;
                    case KeyCode.LeftArrow: pressed = k.leftArrowKey.wasPressedThisFrame; break;
                    case KeyCode.RightArrow: pressed = k.rightArrowKey.wasPressedThisFrame; break;
                    case KeyCode.Backspace: pressed = k.backspaceKey.wasPressedThisFrame; break;
                }

                if (pressed) return true;
            }

            try
            {
                return Input.GetKeyDown(key);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Query whether a KeyCode is currently held down using New Input System with Legacy fallback.
        /// </summary>
        public static bool GetKey(KeyCode key)
        {
            if (Keyboard.current != null)
            {
                var k = Keyboard.current;
                bool pressed = false;
                switch (key)
                {
                    case KeyCode.Q: pressed = k.qKey.isPressed; break;
                    case KeyCode.E: pressed = k.eKey.isPressed; break;
                    case KeyCode.W: pressed = k.wKey.isPressed; break;
                    case KeyCode.A: pressed = k.aKey.isPressed; break;
                    case KeyCode.S: pressed = k.sKey.isPressed; break;
                    case KeyCode.D: pressed = k.dKey.isPressed; break;
                    case KeyCode.R: pressed = k.rKey.isPressed; break;
                    case KeyCode.F: pressed = k.fKey.isPressed; break;
                    case KeyCode.C: pressed = k.cKey.isPressed; break;
                    case KeyCode.V: pressed = k.vKey.isPressed; break;
                    case KeyCode.X: pressed = k.xKey.isPressed; break;
                    case KeyCode.Z: pressed = k.zKey.isPressed; break;
                    case KeyCode.I: pressed = k.iKey.isPressed; break;
                    case KeyCode.M: pressed = k.mKey.isPressed; break;
                    case KeyCode.P: pressed = k.pKey.isPressed; break;
                    case KeyCode.Space: pressed = k.spaceKey.isPressed; break;
                    case KeyCode.Escape: pressed = k.escapeKey.isPressed; break;
                    case KeyCode.Tab: pressed = k.tabKey.isPressed; break;
                    case KeyCode.Return: pressed = k.enterKey.isPressed; break;
                    case KeyCode.Alpha1: pressed = k.digit1Key.isPressed; break;
                    case KeyCode.Alpha2: pressed = k.digit2Key.isPressed; break;
                    case KeyCode.Alpha3: pressed = k.digit3Key.isPressed; break;
                    case KeyCode.Alpha4: pressed = k.digit4Key.isPressed; break;
                    case KeyCode.Alpha5: pressed = k.digit5Key.isPressed; break;
                    case KeyCode.Alpha6: pressed = k.digit6Key.isPressed; break;
                    case KeyCode.Alpha7: pressed = k.digit7Key.isPressed; break;
                    case KeyCode.Alpha8: pressed = k.digit8Key.isPressed; break;
                    case KeyCode.Alpha9: pressed = k.digit9Key.isPressed; break;
                    case KeyCode.Alpha0: pressed = k.digit0Key.isPressed; break;
                    case KeyCode.LeftShift: pressed = k.leftShiftKey.isPressed; break;
                    case KeyCode.RightShift: pressed = k.rightShiftKey.isPressed; break;
                    case KeyCode.LeftControl: pressed = k.leftCtrlKey.isPressed; break;
                    case KeyCode.RightControl: pressed = k.rightCtrlKey.isPressed; break;
                    case KeyCode.LeftAlt: pressed = k.leftAltKey.isPressed; break;
                    case KeyCode.RightAlt: pressed = k.rightAltKey.isPressed; break;
                    case KeyCode.UpArrow: pressed = k.upArrowKey.isPressed; break;
                    case KeyCode.DownArrow: pressed = k.downArrowKey.isPressed; break;
                    case KeyCode.LeftArrow: pressed = k.leftArrowKey.isPressed; break;
                    case KeyCode.RightArrow: pressed = k.rightArrowKey.isPressed; break;
                    case KeyCode.Backspace: pressed = k.backspaceKey.isPressed; break;
                }

                if (pressed) return true;
            }

            try
            {
                return Input.GetKey(key);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Query whether a KeyCode was released this frame using New Input System with Legacy fallback.
        /// </summary>
        public static bool GetKeyUp(KeyCode key)
        {
            if (Keyboard.current != null)
            {
                var k = Keyboard.current;
                bool released = false;
                switch (key)
                {
                    case KeyCode.Q: released = k.qKey.wasReleasedThisFrame; break;
                    case KeyCode.E: released = k.eKey.wasReleasedThisFrame; break;
                    case KeyCode.W: released = k.wKey.wasReleasedThisFrame; break;
                    case KeyCode.A: released = k.aKey.wasReleasedThisFrame; break;
                    case KeyCode.S: released = k.sKey.wasReleasedThisFrame; break;
                    case KeyCode.D: released = k.dKey.wasReleasedThisFrame; break;
                    case KeyCode.R: released = k.rKey.wasReleasedThisFrame; break;
                    case KeyCode.F: released = k.fKey.wasReleasedThisFrame; break;
                    case KeyCode.C: released = k.cKey.wasReleasedThisFrame; break;
                    case KeyCode.V: released = k.vKey.wasReleasedThisFrame; break;
                    case KeyCode.X: released = k.xKey.wasReleasedThisFrame; break;
                    case KeyCode.Z: released = k.zKey.wasReleasedThisFrame; break;
                    case KeyCode.I: released = k.iKey.wasReleasedThisFrame; break;
                    case KeyCode.M: released = k.mKey.wasReleasedThisFrame; break;
                    case KeyCode.P: released = k.pKey.wasReleasedThisFrame; break;
                    case KeyCode.Space: released = k.spaceKey.wasReleasedThisFrame; break;
                    case KeyCode.Escape: released = k.escapeKey.wasReleasedThisFrame; break;
                    case KeyCode.Tab: released = k.tabKey.wasReleasedThisFrame; break;
                    case KeyCode.Return: released = k.enterKey.wasReleasedThisFrame; break;
                    case KeyCode.Alpha1: released = k.digit1Key.wasReleasedThisFrame; break;
                    case KeyCode.Alpha2: released = k.digit2Key.wasReleasedThisFrame; break;
                    case KeyCode.Alpha3: released = k.digit3Key.wasReleasedThisFrame; break;
                    case KeyCode.Alpha4: released = k.digit4Key.wasReleasedThisFrame; break;
                    case KeyCode.Alpha5: released = k.digit5Key.wasReleasedThisFrame; break;
                    case KeyCode.Alpha6: released = k.digit6Key.wasReleasedThisFrame; break;
                    case KeyCode.Alpha7: released = k.digit7Key.wasReleasedThisFrame; break;
                    case KeyCode.Alpha8: released = k.digit8Key.wasReleasedThisFrame; break;
                    case KeyCode.Alpha9: released = k.digit9Key.wasReleasedThisFrame; break;
                    case KeyCode.Alpha0: released = k.digit0Key.wasReleasedThisFrame; break;
                    case KeyCode.LeftShift: released = k.leftShiftKey.wasReleasedThisFrame; break;
                    case KeyCode.RightShift: released = k.rightShiftKey.wasReleasedThisFrame; break;
                    case KeyCode.LeftControl: released = k.leftCtrlKey.wasReleasedThisFrame; break;
                    case KeyCode.RightControl: released = k.rightCtrlKey.wasReleasedThisFrame; break;
                    case KeyCode.LeftAlt: released = k.leftAltKey.wasReleasedThisFrame; break;
                    case KeyCode.RightAlt: released = k.rightAltKey.wasReleasedThisFrame; break;
                    case KeyCode.UpArrow: released = k.upArrowKey.wasReleasedThisFrame; break;
                    case KeyCode.DownArrow: released = k.downArrowKey.wasReleasedThisFrame; break;
                    case KeyCode.LeftArrow: released = k.leftArrowKey.wasReleasedThisFrame; break;
                    case KeyCode.RightArrow: released = k.rightArrowKey.wasReleasedThisFrame; break;
                    case KeyCode.Backspace: released = k.backspaceKey.wasReleasedThisFrame; break;
                }

                if (released) return true;
            }

            try
            {
                return Input.GetKeyUp(key);
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}
