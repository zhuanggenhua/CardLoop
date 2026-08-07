using UnityEngine;
using UnityEngine.InputSystem;

namespace CryingSnow.StackCraft
{
    internal static class StackCraftInput
    {
        public static Vector2 PointerPosition
        {
            get
            {
                if (Pointer.current != null)
                    return Pointer.current.position.ReadValue();

                var touchscreen = Touchscreen.current;
                if (touchscreen != null)
                    return touchscreen.primaryTouch.position.ReadValue();

                return Vector2.zero;
            }
        }

        public static Vector3 PointerPosition3
        {
            get
            {
                Vector2 position = PointerPosition;
                return new Vector3(position.x, position.y, 0f);
            }
        }

        public static Vector2 ScrollDelta
        {
            get
            {
                var mouse = Mouse.current;
                return mouse != null ? mouse.scroll.ReadValue() : Vector2.zero;
            }
        }

        public static bool LeftButtonWasPressedThisFrame
        {
            get
            {
                var mouse = Mouse.current;
                if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                    return true;

                var touchscreen = Touchscreen.current;
                return touchscreen != null && touchscreen.primaryTouch.press.wasPressedThisFrame;
            }
        }

        public static bool LeftButtonWasReleasedThisFrame
        {
            get
            {
                var mouse = Mouse.current;
                if (mouse != null && mouse.leftButton.wasReleasedThisFrame)
                    return true;

                var touchscreen = Touchscreen.current;
                return touchscreen != null && touchscreen.primaryTouch.press.wasReleasedThisFrame;
            }
        }

        public static bool MiddleButtonWasPressedThisFrame
        {
            get
            {
                var mouse = Mouse.current;
                return mouse != null && mouse.middleButton.wasPressedThisFrame;
            }
        }

        public static bool MiddleButtonWasReleasedThisFrame
        {
            get
            {
                var mouse = Mouse.current;
                return mouse != null && mouse.middleButton.wasReleasedThisFrame;
            }
        }

        public static bool CancelWasPressedThisFrame
        {
            get
            {
                var keyboard = Keyboard.current;
                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                    return true;

                var gamepad = Gamepad.current;
                return gamepad != null && gamepad.buttonEast.wasPressedThisFrame;
            }
        }
    }
}
