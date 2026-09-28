using System.Runtime.InteropServices;

namespace KeyboardSwitcher;

public sealed class TextReplacer
{
    private const uint INPUT_KEYBOARD = 1;

    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_SCANCODE = 0x0008;

    private const ushort VK_BACK = 0x08;

    private const ulong ConverterMarkerValue =
        0x4B53434F4E564552UL;

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(
        uint nInputs,
        INPUT[] pInputs,
        int cbSize);

    public bool DeleteCharacters(int count)
    {
        if (count <= 0)
            return true;

        var inputs =
            new INPUT[count * 2];

        for (int i = 0; i < count; i++)
        {
            inputs[i * 2] =
                CreateBackspace(false);

            inputs[i * 2 + 1] =
                CreateBackspace(true);
        }

        uint sent =
            SendInput(
                (uint)inputs.Length,
                inputs,
                Marshal.SizeOf<INPUT>());

        DebugLog.Write(
            $"TEXTREPLACER: DeleteCharacters " +
            $"Count={count} " +
            $"Sent={sent}/{inputs.Length}");

        return sent == inputs.Length;
    }

    private static INPUT CreateBackspace(
        bool keyUp)
    {
        return new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = VK_BACK,
                    wScan = 0,
                    dwFlags =
                        keyUp
                            ? KEYEVENTF_KEYUP
                            : 0,
                    time = 0,
                    dwExtraInfo =
                        new UIntPtr(
                            ConverterMarkerValue)
                }
            }
        };
    }
}