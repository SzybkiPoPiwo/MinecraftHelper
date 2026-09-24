using System;
using System.Runtime.InteropServices;

namespace MinecraftHelper.Services
{
    internal static class NativeInput
    {
        private const uint InputMouse = 0;
        private const uint InputKeyboard = 1;

        private const uint MouseEventMove = 0x0001;
        private const uint MouseEventLeftDown = 0x0002;
        private const uint MouseEventLeftUp = 0x0004;
        private const uint MouseEventRightDown = 0x0008;
        private const uint MouseEventRightUp = 0x0010;
        private const uint MouseEventMoveNoCoalesce = 0x2000;
        private const uint KeyEventKeyUp = 0x0002;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint inputCount, [In] INPUT[] inputs, int inputSize);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetCursorPos(int x, int y);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint Type;
            public INPUTUNION Data;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUTUNION
        {
            [FieldOffset(0)]
            public MOUSEINPUT Mouse;

            [FieldOffset(0)]
            public KEYBDINPUT Keyboard;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int Dx;
            public int Dy;
            public uint MouseData;
            public uint Flags;
            public uint Time;
            public UIntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort VirtualKey;
            public ushort ScanCode;
            public uint Flags;
            public uint Time;
            public UIntPtr ExtraInfo;
        }

        public static bool SendMouseClick(bool leftButton, bool holdPulseMode)
        {
            uint down = leftButton ? MouseEventLeftDown : MouseEventRightDown;
            uint up = leftButton ? MouseEventLeftUp : MouseEventRightUp;

            // A complete click is submitted as one batch. Windows cannot interleave
            // unrelated input between its DOWN/UP pair, which prevents stuck buttons.
            INPUT[] inputs = holdPulseMode
                ? new[] { CreateMouseInput(up), CreateMouseInput(down) }
                : new[] { CreateMouseInput(down), CreateMouseInput(up) };

            uint sent = SendRaw(inputs);
            if (sent == inputs.Length)
                return true;

            // Best-effort recovery if Windows accepted only part of the batch.
            // A normal click must finish UP; a hold pulse must finish DOWN.
            Send(new[] { CreateMouseInput(holdPulseMode ? down : up) });
            return false;
        }

        public static bool SendMouseButton(bool leftButton, bool down)
        {
            uint flags = leftButton
                ? down ? MouseEventLeftDown : MouseEventLeftUp
                : down ? MouseEventRightDown : MouseEventRightUp;

            return Send(new[] { CreateMouseInput(flags) });
        }

        public static bool SendMouseMoveRelative(int deltaX, int deltaY)
        {
            if (deltaX == 0 && deltaY == 0)
                return true;

            return Send(new[] { CreateMouseInput(MouseEventMove | MouseEventMoveNoCoalesce, deltaX, deltaY) });
        }

        public static bool SendKey(int virtualKey, bool down)
        {
            return Send(new[] { CreateKeyboardInput(virtualKey, down) });
        }

        public static bool SendKeyChord(int modifierVirtualKey, int virtualKey)
        {
            INPUT[] inputs =
            {
                CreateKeyboardInput(modifierVirtualKey, down: true),
                CreateKeyboardInput(virtualKey, down: true),
                CreateKeyboardInput(virtualKey, down: false),
                CreateKeyboardInput(modifierVirtualKey, down: false)
            };

            uint sent = SendRaw(inputs);
            if (sent == inputs.Length)
                return true;

            // Never leave Ctrl or the action key held after a partially accepted batch.
            Send(new[]
            {
                CreateKeyboardInput(virtualKey, down: false),
                CreateKeyboardInput(modifierVirtualKey, down: false)
            });
            return false;
        }

        public static bool SetCursorPosition(int x, int y)
        {
            return SetCursorPos(x, y);
        }

        private static INPUT CreateMouseInput(uint flags, int deltaX = 0, int deltaY = 0)
        {
            return new INPUT
            {
                Type = InputMouse,
                Data = new INPUTUNION
                {
                    Mouse = new MOUSEINPUT
                    {
                        Dx = deltaX,
                        Dy = deltaY,
                        MouseData = 0,
                        Flags = flags,
                        Time = 0,
                        ExtraInfo = UIntPtr.Zero
                    }
                }
            };
        }

        private static INPUT CreateKeyboardInput(int virtualKey, bool down)
        {
            return new INPUT
            {
                Type = InputKeyboard,
                Data = new INPUTUNION
                {
                    Keyboard = new KEYBDINPUT
                    {
                        VirtualKey = (ushort)(virtualKey & 0xFFFF),
                        ScanCode = 0,
                        Flags = down ? 0 : KeyEventKeyUp,
                        Time = 0,
                        ExtraInfo = UIntPtr.Zero
                    }
                }
            };
        }

        private static bool Send(INPUT[] inputs)
        {
            return SendRaw(inputs) == inputs.Length;
        }

        private static uint SendRaw(INPUT[] inputs)
        {
            return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        }
    }
}
