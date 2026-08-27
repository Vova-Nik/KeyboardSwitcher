using System.Runtime.InteropServices;

namespace KeyboardSwitcher;

public enum LanguageKey
{
    English,
    Russian,
    Ukrainian
}


public sealed class KeyboardHook : IDisposable
{
    // ============================================================
    // Windows constants
    // ============================================================

    private const int WH_KEYBOARD_LL = 13;

    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;

    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    private const int VK_CAPITAL = 0x14;

    private const int VK_A = 0x41;
    private const int VK_Z = 0x5A;
    private const int VK_Q = 0x51;


    // ============================================================
    // Windows API
    // ============================================================

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowsHookEx(
        int idHook,
        LowLevelKeyboardProc lpfn,
        IntPtr hMod,
        uint dwThreadId);


    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(
        IntPtr hhk);


    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(
        IntPtr hhk,
        int nCode,
        IntPtr wParam,
        IntPtr lParam);


    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(
        string? lpModuleName);


    [DllImport("user32.dll")]
    private static extern short GetKeyState(
        int nVirtKey);


    [DllImport("user32.dll")]
    private static extern uint SendInput(
        uint nInputs,
        INPUT[] pInputs,
        int cbSize);


    // ============================================================
    // SendInput structures
    // ============================================================

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


    private const uint INPUT_KEYBOARD = 1;

    private const uint KEYEVENTF_KEYUP = 0x0002;


    // ============================================================
    // Hook structures
    // ============================================================

    private delegate IntPtr LowLevelKeyboardProc(
        int nCode,
        IntPtr wParam,
        IntPtr lParam);


    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;

        public uint scanCode;

        public uint flags;

        public uint time;

        public UIntPtr dwExtraInfo;
    }


    // ============================================================
    // State
    // ============================================================

    private IntPtr _hookId =
        IntPtr.Zero;


    private LowLevelKeyboardProc? _hookProc;


    private bool _capsModifierDown;


    private bool _disposed;


    public bool IsRunning =>
        _hookId != IntPtr.Zero;


    // ============================================================
    // Events
    // ============================================================

    public event Action<LanguageKey>? LanguageRequested;


    // ============================================================
    // Start
    // ============================================================

    public bool Start()
    {
        if (_disposed)
            throw new ObjectDisposedException(
                nameof(KeyboardHook));

        if (IsRunning)
            return true;

        // Завжди починаємо з CapsLock = OFF.
        ForceCapsLockOff();

        _hookProc = HookCallback;

        _hookId =
            SetWindowsHookEx(
                WH_KEYBOARD_LL,
                _hookProc,
                GetModuleHandle(null),
                0);

        if (_hookId == IntPtr.Zero)
        {
            _hookProc = null;
            return false;
        }

        _capsModifierDown = false;

        return true;
    }


    // ============================================================
    // Stop
    // ============================================================

    public void Stop()
    {
        if (!IsRunning)
            return;


        // --------------------------------------------------------
        // Спочатку знімаємо hook.
        // --------------------------------------------------------

        UnhookWindowsHookEx(
            _hookId);


        _hookId =
            IntPtr.Zero;


        _hookProc = null;


        _capsModifierDown = false;


        // --------------------------------------------------------
        // Тепер можна безпечно привести CapsLock у OFF.
        // --------------------------------------------------------

        ForceCapsLockOff();
    }


    // ============================================================
    // Hook callback
    // ============================================================

    private IntPtr HookCallback(
        int nCode,
        IntPtr wParam,
        IntPtr lParam)
    {
        if (nCode < 0)
        {
            return CallNextHookEx(
                _hookId,
                nCode,
                wParam,
                lParam);
        }


        int message =
            wParam.ToInt32();


        KBDLLHOOKSTRUCT data =
            Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(
                lParam);


        int vk =
            unchecked(
                (int)data.vkCode);


        bool keyDown =
            message == WM_KEYDOWN ||
            message == WM_SYSKEYDOWN;


        bool keyUp =
            message == WM_KEYUP ||
            message == WM_SYSKEYUP;


        // ========================================================
        // CAPSLOCK
        // ========================================================

        if (vk == VK_CAPITAL)
        {
            if (keyDown)
            {
                _capsModifierDown = true;


                // CapsLock як звичайна клавіша
                // повністю поглинається.
                return (IntPtr)1;
            }


            if (keyUp)
            {
                _capsModifierDown = false;


                return (IntPtr)1;
            }
        }


        // ========================================================
        // CAPS + A
        // ========================================================

        if (_capsModifierDown &&
            vk == VK_A)
        {
            if (keyDown)
            {
                LanguageRequested?.Invoke(
                    LanguageKey.English);
            }


            return (IntPtr)1;
        }


        // ========================================================
        // CAPS + Z
        // ========================================================

        if (_capsModifierDown &&
            vk == VK_Z)
        {
            if (keyDown)
            {
                LanguageRequested?.Invoke(
                    LanguageKey.Russian);
            }


            return (IntPtr)1;
        }


        // ========================================================
        // CAPS + Q
        // ========================================================

        if (_capsModifierDown &&
            vk == VK_Q)
        {
            if (keyDown)
            {
                LanguageRequested?.Invoke(
                    LanguageKey.Ukrainian);
            }


            return (IntPtr)1;
        }


        // ========================================================
        // Все інше пропускаємо.
        // ========================================================

        return CallNextHookEx(
            _hookId,
            nCode,
            wParam,
            lParam);
    }


    // ============================================================
    // Force CapsLock OFF
    // ============================================================

    private static void ForceCapsLockOff()
    {
        bool capsIsOn =
            (GetKeyState(VK_CAPITAL) & 1) != 0;


        if (!capsIsOn)
            return;


        INPUT[] inputs =
        {
            new INPUT
            {
                type = INPUT_KEYBOARD,

                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = VK_CAPITAL,

                        wScan = 0,

                        dwFlags = 0,

                        time = 0,

                        dwExtraInfo =
                            UIntPtr.Zero
                    }
                }
            },

            new INPUT
            {
                type = INPUT_KEYBOARD,

                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = VK_CAPITAL,

                        wScan = 0,

                        dwFlags =
                            KEYEVENTF_KEYUP,

                        time = 0,

                        dwExtraInfo =
                            UIntPtr.Zero
                    }
                }
            }
        };


        SendInput(
            (uint)inputs.Length,

            inputs,

            Marshal.SizeOf<INPUT>());
    }


    // ============================================================
    // Dispose
    // ============================================================

    public void Dispose()
    {
        if (_disposed)
            return;


        Stop();


        _disposed = true;


        GC.SuppressFinalize(this);
    }
}