using UnityEngine.InputSystem;

namespace MetalRaptors
{
    public static class MenuInput
    {
        public static int ReadStep()
        {
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;

            bool forward = (kb != null && kb.downArrowKey.wasPressedThisFrame)
                           || (pad != null && pad.dpad.down.wasPressedThisFrame);
            bool back = (kb != null && kb.upArrowKey.wasPressedThisFrame)
                        || (pad != null && pad.dpad.up.wasPressedThisFrame);

            if (forward == back) return 0;
            return forward ? 1 : -1;
        }

        public static int ReadAdjust()
        {
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;

            bool forward = (kb != null && kb.rightArrowKey.wasPressedThisFrame)
                           || (pad != null && pad.dpad.right.wasPressedThisFrame);
            bool back = (kb != null && kb.leftArrowKey.wasPressedThisFrame)
                        || (pad != null && pad.dpad.left.wasPressedThisFrame);

            if (forward == back) return 0;
            return forward ? 1 : -1;
        }

        public static bool ReadSubmit()
        {
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;
            return (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame
                                                                   || kb.spaceKey.wasPressedThisFrame))
                   || (pad != null && pad.buttonSouth.wasPressedThisFrame);
        }

        public static bool ReadAnyKey()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.anyKey.wasPressedThisFrame) return true;

            Gamepad pad = Gamepad.current;
            if (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame
                                || pad.buttonWest.wasPressedThisFrame || pad.buttonNorth.wasPressedThisFrame
                                || pad.startButton.wasPressedThisFrame)) return true;

            Mouse mouse = Mouse.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame;
        }

        public static bool ReadSkip()
        {
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;
            return (kb != null && kb.spaceKey.wasPressedThisFrame)
                   || (pad != null && pad.buttonSouth.wasPressedThisFrame);
        }

        public static bool ReadCancel()
        {
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;
            return (kb != null && kb.escapeKey.wasPressedThisFrame)
                   || (pad != null && pad.buttonEast.wasPressedThisFrame);
        }
    }
}
