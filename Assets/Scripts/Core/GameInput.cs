using System;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CastleOfTheD20.Core
{
    /// <summary>
    /// Unified cross-compatible input bridge supporting both Unity's New Input System (UnityEngine.InputSystem)
    /// and the legacy Input Manager (UnityEngine.Input) without throwing InvalidOperationException.
    /// Automatically detects which input subsystem is active in Player Settings.
    /// </summary>
    public static class GameInput
    {
        #region Mouse Queries

        /// <summary>
        /// True during the frame the user pressed the primary mouse button (Left Click).
        /// </summary>
        public static bool GetLeftMouseButtonDown()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.leftButton.wasPressedThisFrame;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            try
            {
                return Input.GetMouseButtonDown(0);
            }
            catch (InvalidOperationException)
            {
                // Active input handling is set to New Input System, but preprocessor flag was missing
                return false;
            }
#else
            return false;
#endif
        }

        /// <summary>
        /// True while the primary mouse button (Left Click) is held down.
        /// </summary>
        public static bool GetLeftMouseButton()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.leftButton.isPressed;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            try
            {
                return Input.GetMouseButton(0);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
#else
            return false;
#endif
        }

        /// <summary>
        /// True during the frame the user pressed the secondary mouse button (Right Click).
        /// </summary>
        public static bool GetRightMouseButtonDown()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.rightButton.wasPressedThisFrame;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            try
            {
                return Input.GetMouseButtonDown(1);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
#else
            return false;
#endif
        }

        /// <summary>
        /// True while the secondary mouse button (Right Click) is held down.
        /// </summary>
        public static bool GetRightMouseButton()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.rightButton.isPressed;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            try
            {
                return Input.GetMouseButton(1);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
#else
            return false;
#endif
        }

        /// <summary>
        /// Returns the mouse movement delta vector (X = Horizontal, Y = Vertical) since last frame.
        /// Scaled to consistent units across New Input System and Legacy Input Manager.
        /// </summary>
        public static Vector2 GetMouseDelta()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.delta.ReadValue();
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            try
            {
                return new Vector2(Input.GetAxisRaw("Mouse X") * 15f, Input.GetAxisRaw("Mouse Y") * 15f);
            }
            catch (InvalidOperationException)
            {
                return Vector2.zero;
            }
#else
            return Vector2.zero;
#endif
        }

        /// <summary>
        /// Current screen coordinates of the mouse cursor in pixels.
        /// </summary>
        public static Vector2 GetMousePosition()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.position.ReadValue();
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            try
            {
                return Input.mousePosition;
            }
            catch (InvalidOperationException)
            {
                return Vector2.zero;
            }
#else
            return Vector2.zero;
#endif
        }

        #endregion

        #region Keyboard & Locomotion Queries

        /// <summary>
        /// Returns a normalized 2D movement vector (X = Horizontal [-1..1], Y = Vertical [-1..1])
        /// polled from WASD, arrow keys, or gamepad thumbsticks.
        /// </summary>
        public static Vector2 GetMovementVector()
        {
            float horizontal = 0f;
            float vertical = 0f;

#if ENABLE_INPUT_SYSTEM
            // 1. New Input System - Keyboard
            if (Keyboard.current != null)
            {
                var k = Keyboard.current;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) horizontal += 1f;
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) horizontal -= 1f;
                if (k.wKey.isPressed || k.upArrowKey.isPressed) vertical += 1f;
                if (k.sKey.isPressed || k.downArrowKey.isPressed) vertical -= 1f;
            }

            // 2. New Input System - Gamepad (optional support)
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
#endif

#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            // Legacy Input Manager fallback if enabled
            try
            {
                float axisH = Input.GetAxisRaw("Horizontal");
                float axisV = Input.GetAxisRaw("Vertical");

                if (Mathf.Abs(axisH) > 0.01f) horizontal = axisH;
                if (Mathf.Abs(axisV) > 0.01f) vertical = axisV;

                if (Mathf.Approximately(horizontal, 0f))
                {
                    if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) horizontal += 1f;
                    if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) horizontal -= 1f;
                }

                if (Mathf.Approximately(vertical, 0f))
                {
                    if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) vertical += 1f;
                    if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) vertical -= 1f;
                }
            }
            catch (InvalidOperationException)
            {
                // Handled gracefully when running in New Input System mode
            }
#endif

            return new Vector2(Mathf.Clamp(horizontal, -1f, 1f), Mathf.Clamp(vertical, -1f, 1f));
        }

        #endregion
    }
}
