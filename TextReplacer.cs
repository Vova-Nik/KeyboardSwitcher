using System.Runtime.InteropServices;

namespace KeyboardSwitcher;

public sealed class TextReplacer
{
    private const uint INPUT_KEYBOARD = 1;

    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_SCANCODE = 0x0008;

    private const ushort VK_BACK = 0x08;

    private const uint KEYEVENTF_UNICODE = 0x0004;

    //private const ulong ConverterMarkerValue =
    //    0x4B53434F4E564552UL;


    //[StructLayout(LayoutKind.Sequential)]
    //private struct INPUT
    //{
    //    public uint type;
    //    public KEYBDINPUT ki;
    //}

    //[StructLayout(LayoutKind.Sequential)]
    //private struct KEYBDINPUT
    //{
    //    public ushort wVk;
    //    public ushort wScan;
    //    public uint dwFlags;
    //    public uint time;
    //    public UIntPtr dwExtraInfo;
    //}

    [StructLayout(LayoutKind.Explicit, Size = 40)]
    private struct INPUT
    {
        [FieldOffset(0)]
        public uint type;

        [FieldOffset(8)]
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

        //uint sent =
        //    SendInput(
        //        (uint)inputs.Length,
        //        inputs,
        //        Marshal.SizeOf<INPUT>());

        //DebugLog.Write(
        //    $"TEXTREPLACER: DeleteCharacters " +
        //    $"Count={count} " +
        //    $"Sent={sent}/{inputs.Length}");

        DebugLog.Write(
            $"TEXTREPLACER: INPUT size=" +
            $"{Marshal.SizeOf<INPUT>()} " +
            $"KEYBDINPUT size=" +
            $"{Marshal.SizeOf<KEYBDINPUT>()}");

        uint sent =
            SendInput(
                (uint)inputs.Length,
                inputs,
                Marshal.SizeOf<INPUT>());

        int error =
            sent == inputs.Length
                ? 0
                : Marshal.GetLastWin32Error();

        DebugLog.Write(
            $"TEXTREPLACER: DeleteCharacters " +
            $"Count={count} " +
            $"Sent={sent}/{inputs.Length} " +
            $"Error={error}");

        return sent == inputs.Length;
    }

    //private static INPUT CreateBackspace(
    //    bool keyUp)
    //{
    //    return new INPUT
    //    {
    //        type = INPUT_KEYBOARD,
    //        U = new InputUnion
    //        {
    //            ki = new KEYBDINPUT
    //            {
    //                wVk = VK_BACK,
    //                wScan = 0,
    //                dwFlags =
    //                    keyUp
    //                        ? KEYEVENTF_KEYUP
    //                        : 0,
    //                time = 0,
    //                dwExtraInfo =
    //                    new UIntPtr(
    //                        ConverterMarkerValue)
    //            }
    //        }
    //    };
    //}
    private static INPUT CreateBackspace(
        bool keyUp)
    {
        return new INPUT
        {
            type = INPUT_KEYBOARD,
            ki = new KEYBDINPUT
            {
                wVk = VK_BACK,
                wScan = 0,
                dwFlags =
                    keyUp
                        ? KEYEVENTF_KEYUP
                        : 0,
                time = 0,
                //dwExtraInfo =
                //    new UIntPtr(
                //        ConverterMarkerValue)
                dwExtraInfo =
                          InputInjection.ConverterMarker

            }
        };
    }

    public bool InsertText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return true;

        var inputs =
            new List<INPUT>();

        foreach (char character in text)
        {
            AddUnicodeCharacter(
                inputs,
                character);
        }

        if (inputs.Count == 0)
            return true;

        INPUT[] inputArray =
            inputs.ToArray();

        uint sent =
            SendInput(
                (uint)inputArray.Length,
                inputArray,
                Marshal.SizeOf<INPUT>());

        int error =
            sent == inputArray.Length
                ? 0
                : Marshal.GetLastWin32Error();

        DebugLog.Write(
            $"TEXTREPLACER: InsertText " +
            $"Text=\"{text}\" " +
            $"Inputs={inputArray.Length} " +
            $"Sent={sent} " +
            $"Error={error}");

        return sent == inputArray.Length;
    }

    private static void AddUnicodeCharacter(
        List<INPUT> inputs,
        char character)
    {
        inputs.Add(
            new INPUT
            {
                type = INPUT_KEYBOARD,
                ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = character,
                    dwFlags = KEYEVENTF_UNICODE,
                    time = 0,
                    dwExtraInfo =
                        InputInjection.ConverterMarker
                }
            });

        inputs.Add(
            new INPUT
            {
                type = INPUT_KEYBOARD,
                ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = character,
                    dwFlags =
                        KEYEVENTF_UNICODE |
                        KEYEVENTF_KEYUP,
                    time = 0,
                    dwExtraInfo =
                        InputInjection.ConverterMarker
                }
            });
    }

}