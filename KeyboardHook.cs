using System.Runtime.InteropServices;

namespace KeyboardSwitcher;

public enum LanguageKey
{
    English,
    Russian,
    Ukrainian
}

public enum LanguageAction
{
    SwitchLanguage,
    ShiftKey
}

public sealed class KeyboardHook : IDisposable
{
    // Windows constants
    private const int WH_KEYBOARD_LL = 13;

    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    private const int VK_CAPITAL = 0x14;

    private const int VK_A = 0x41;
    private const int VK_Z = 0x5A;
    private const int VK_Q = 0x51;

    private const int VK_SHIFT = 0x10;
    private const int VK_LSHIFT = 0xA0;
    private const int VK_RSHIFT = 0xA1;

    private const int VK_CONTROL = 0x11;
    private const int VK_LCONTROL = 0xA2;
    private const int VK_RCONTROL = 0xA3;

    private const int VK_MENU = 0x12;
    private const int VK_LMENU = 0xA4;
    private const int VK_RMENU = 0xA5;

    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;

    private const int VK_APPS = 0x5D;

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    private const uint KEYEVENTF_SCANCODE = 0x0008;
    private const ushort SCAN_LSHIFT = 0x2A;

    // LLKHF_INJECTED
    private const uint LLKHF_INJECTED = 0x00000010;

    // Маркер наших SendInput-подій.
    private static readonly UIntPtr InjectionMarker =
        new UIntPtr(0x4B535748u);


    // Windows API
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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(
        uint nInputs,
        INPUT[] pInputs,
        int cbSize);


    // SendInput structures

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
        public MOUSEINPUT mi;

        [FieldOffset(0)]
        public KEYBDINPUT ki;

        [FieldOffset(0)]
        public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
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

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }


    // Hook structures
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


    // State
    private IntPtr _hookId = IntPtr.Zero;
    private LowLevelKeyboardProc? _hookProc;

    private bool _capsModifierDown;

    // Таймер підказки при утриманні чистого CapsLock.
    private readonly System.Windows.Forms.Timer _capsHoldTimer;
    private bool _capsHelpShown;

    private bool _leftShiftDown;
    private bool _rightShiftDown;

    // Клавіші, які зараз імітуються як Shift+Key.
    private readonly HashSet<int> _shiftedKeys = new();

    // Клавіші A/Z/Q, які були перехоплені як мовні команди.
    private readonly HashSet<int> _languageCommandKeys = new();

    // Чи натиснутий нами синтетичний Shift.
    private bool _syntheticShiftDown;

    private bool _disposed;

    public bool IsRunning =>
        _hookId != IntPtr.Zero;

    public bool IsCapsModifierDown =>
        _capsModifierDown;


    // ------------------------------------------------------------
    // Events
    // ------------------------------------------------------------

    // Запит: "що робити з A/Z/Q?"
    //
    // TrayApplication повертає:
    // SwitchLanguage -> поглинути клавішу та перемкнути мову
    // ShiftKey       -> поводитися як Shift+Key
    public event Func<LanguageKey, LanguageAction>? LanguageRequested;

    // Показати підказку після тривалого утримання CapsLock.
    public event Action? ShortcutHintRequested;

    // CapsLock натиснутий / відпущений.
    public event Action? CapsLockPressed;
    public event Action? CapsLockReleased;

    // Будь-яка клавіша після CapsLock.
    public event Action? CapsLockActivity;


    // ------------------------------------------------------------
    // Constructor
    // ------------------------------------------------------------

    public KeyboardHook()
    {
        _capsHoldTimer = new System.Windows.Forms.Timer
        {
            Interval = 1200
        };

        _capsHoldTimer.Tick += OnCapsHoldTimerTick;
    }


    // ------------------------------------------------------------
    // Start
    // ------------------------------------------------------------

    public bool Start()
    {
        if (_disposed)
            throw new ObjectDisposedException(
                nameof(KeyboardHook));

        if (IsRunning)
            return true;

        ForceCapsLockOff();

        _capsModifierDown = false;
        _capsHelpShown = false;
        _capsHoldTimer.Stop();

        _leftShiftDown = false;
        _rightShiftDown = false;

        _shiftedKeys.Clear();
        _languageCommandKeys.Clear();

        _syntheticShiftDown = false;

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

        return true;
    }


    // ------------------------------------------------------------
    // Stop
    // ------------------------------------------------------------

    public void Stop()
    {
        _capsHoldTimer.Stop();
        _capsHelpShown = false;

        if (!IsRunning)
            return;

        UnhookWindowsHookEx(_hookId);

        _hookId = IntPtr.Zero;
        _hookProc = null;

        // Hook уже знятий, тому можна безпечно
        // відпустити синтетичний Shift.
        if (_syntheticShiftDown)
        {
            SendInput(
                1,
                new[]
                {
                    CreateShiftInput(true)
                },
                Marshal.SizeOf<INPUT>());

            _syntheticShiftDown = false;
        }

        _capsModifierDown = false;
        _capsHelpShown = false;

        _leftShiftDown = false;
        _rightShiftDown = false;

        _shiftedKeys.Clear();
        _languageCommandKeys.Clear();

        ForceCapsLockOff();
    }


    // ------------------------------------------------------------
    // Hook callback
    // ------------------------------------------------------------

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

        KBDLLHOOKSTRUCT data =
            Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(
                lParam);

        // Це наші власні SendInput-події.
        // Їх не можна повторно обробляти як Caps/Shift.
        if (data.dwExtraInfo == InjectionMarker)
        {
            return CallNextHookEx(
                _hookId,
                nCode,
                wParam,
                lParam);
        }

        int message =
            wParam.ToInt32();

        int vk =
            unchecked(
                (int)data.vkCode);

        bool keyDown =
            message == WM_KEYDOWN ||
            message == WM_SYSKEYDOWN;

        bool keyUp =
            message == WM_KEYUP ||
            message == WM_SYSKEYUP;

        // --------------------------------------------------------
        // Відстеження фізичного Shift
        // --------------------------------------------------------

        UpdatePhysicalShiftState(
            vk,
            keyDown,
            keyUp);

        // --------------------------------------------------------
        // CAPSLOCK
        // --------------------------------------------------------

        if (vk == VK_CAPITAL)
        {
            if (keyDown)
            {
                if (!_capsModifierDown)
                {
                    _capsModifierDown = true;
                    _capsHelpShown = false;

                    _capsHoldTimer.Start();

                    CapsLockPressed?.Invoke();
                }

                // CapsLock повністю поглинаємо.
                return (IntPtr)1;
            }

            if (keyUp)
            {
                _capsModifierDown = false;

                _capsHoldTimer.Stop();
                _capsHelpShown = false;

                CapsLockReleased?.Invoke();

                return (IntPtr)1;
            }
        }

        // --------------------------------------------------------
        // Якщо це key-up вже перехопленої мовної команди
        // --------------------------------------------------------

        if (keyUp &&
            _languageCommandKeys.Contains(vk))
        {
            _languageCommandKeys.Remove(vk);

            return (IntPtr)1;
        }

        // --------------------------------------------------------
        // Якщо це key-up клавіші, для якої ми створили Shift
        // --------------------------------------------------------

        if (keyUp &&
            _shiftedKeys.Contains(vk))
        {
            bool handled =
                SendShiftedKeyUp(vk);

            if (handled)
                return (IntPtr)1;

            // Якщо не вдалося коректно згенерувати
            // key-up — краще передати оригінальну клавішу.
        }

        // --------------------------------------------------------
        // Якщо Caps не натиснутий — звичайна клавіатура
        // --------------------------------------------------------

        if (!_capsModifierDown)
        {
            return CallNextHookEx(
                _hookId,
                nCode,
                wParam,
                lParam);
        }

        // --------------------------------------------------------
        // Будь-яка клавіша після Caps скасовує майбутню
        // підказку.
        // --------------------------------------------------------

        if (keyDown)
        {
            _capsHoldTimer.Stop();

            CapsLockActivity?.Invoke();
        }

        // --------------------------------------------------------
        // A / Z / Q — спеціальні мовні клавіші
        // --------------------------------------------------------

        if (TryGetLanguageKey(
                vk,
                out LanguageKey language))
        {
            if (keyDown)
            {
                // Якщо фізичний Shift уже натиснутий,
                // Caps не повинен перетворювати A/Z/Q
                // на мовну команду.
                // Це звичайна комбінація Shift + клавіша.
                if (IsPhysicalShiftDown())
                {
                    return CallNextHookEx(
                        _hookId,
                        nCode,
                        wParam,
                        lParam);
                }

                // Якщо ця клавіша вже була перехоплена,
                // це autorepeat. Повторно перемикати мову
                // не потрібно.
                if (_languageCommandKeys.Contains(vk))
                {
                    return (IntPtr)1;
                }

                LanguageAction action =
                    LanguageRequested?.Invoke(language)
                    ?? LanguageAction.SwitchLanguage;

                if (action ==
                    LanguageAction.SwitchLanguage)
                {
                    _languageCommandKeys.Add(vk);

                    return (IntPtr)1;
                }

                // ShiftKey:
                // падаємо нижче і обробляємо A/Z/Q
                // як звичайну Shift-комбінацію.
            }
        }

        // --------------------------------------------------------
        // Shift, Ctrl, Alt, Win тощо самі не перетворюємо.
        //
        // Наприклад:
        // Caps + Ctrl + C
        //
        // Ctrl проходить нормально,
        // а C нижче буде перетворений у Shift+C.
        // --------------------------------------------------------

        if (IsModifierKey(vk))
        {
            return CallNextHookEx(
                _hookId,
                nCode,
                wParam,
                lParam);
        }

        // --------------------------------------------------------
        // Caps працює як Shift
        // --------------------------------------------------------

        if (keyDown)
        {
            // Якщо фізичний Shift уже натиснутий,
            // другого Shift не додаємо.
            if (IsPhysicalShiftDown())
            {
                return CallNextHookEx(
                    _hookId,
                    nCode,
                    wParam,
                    lParam);
            }

            bool handled =
                SendShiftedKeyDown(vk);

            if (handled)
                return (IntPtr)1;
        }

        return CallNextHookEx(
            _hookId,
            nCode,
            wParam,
            lParam);
    }


    // ------------------------------------------------------------
    // CapsLock hold hint
    // ------------------------------------------------------------

    private void OnCapsHoldTimerTick(
        object? sender,
        EventArgs e)
    {
        _capsHoldTimer.Stop();

        if (!_capsModifierDown || _capsHelpShown)
            return;

        _capsHelpShown = true;

        ShortcutHintRequested?.Invoke();
    }


    // ------------------------------------------------------------
    // Language key detection
    // ------------------------------------------------------------

    private static bool TryGetLanguageKey(
        int vk,
        out LanguageKey language)
    {
        switch (vk)
        {
            case VK_A:
                language = LanguageKey.English;
                return true;

            case VK_Z:
                language = LanguageKey.Russian;
                return true;

            case VK_Q:
                language = LanguageKey.Ukrainian;
                return true;

            default:
                language = default;
                return false;
        }
    }


    // ------------------------------------------------------------
    // Physical Shift state
    // ------------------------------------------------------------

    private void UpdatePhysicalShiftState(
        int vk,
        bool keyDown,
        bool keyUp)
    {
        if (keyDown)
        {
            if (vk == VK_LSHIFT)
                _leftShiftDown = true;

            if (vk == VK_RSHIFT)
                _rightShiftDown = true;
        }

        if (keyUp)
        {
            if (vk == VK_LSHIFT)
                _leftShiftDown = false;

            if (vk == VK_RSHIFT)
                _rightShiftDown = false;
        }
    }

    private bool IsPhysicalShiftDown()
    {
        return _leftShiftDown ||
               _rightShiftDown;
    }


    // ------------------------------------------------------------
    // Modifier detection
    // ------------------------------------------------------------

    private static bool IsModifierKey(
        int vk)
    {
        return vk == VK_SHIFT ||
               vk == VK_LSHIFT ||
               vk == VK_RSHIFT ||

               vk == VK_CONTROL ||
               vk == VK_LCONTROL ||
               vk == VK_RCONTROL ||

               vk == VK_MENU ||
               vk == VK_LMENU ||
               vk == VK_RMENU ||

               vk == VK_LWIN ||
               vk == VK_RWIN ||

               vk == VK_APPS;
    }


    // ------------------------------------------------------------
    // Caps-as-Shift
    // ------------------------------------------------------------

    private bool SendShiftedKeyDown(
        int vk)
    {
        // Autorepeat уже натиснутої клавіші.
        if (_shiftedKeys.Contains(vk))
        {
            return SendKey(
                vk,
                false);
        }

        if (!_syntheticShiftDown)
        {
            INPUT[] inputs =
            {
                CreateShiftInput(false),
                CreateKeyInput(vk, false)
            };

            uint sent =
                SendInput(
                    (uint)inputs.Length,
                    inputs,
                    Marshal.SizeOf<INPUT>());

            if (sent != inputs.Length)
                return false;

            _syntheticShiftDown = true;
        }
        else
        {
            if (!SendKey(
                    vk,
                    false))
            {
                return false;
            }
        }

        _shiftedKeys.Add(vk);

        return true;
    }


    private bool SendShiftedKeyUp(
        int vk)
    {
        if (!_shiftedKeys.Contains(vk))
            return false;

        bool lastKey =
            _shiftedKeys.Count == 1;

        if (lastKey)
        {
            INPUT[] inputs =
            {
                CreateKeyInput(
                    vk,
                    true),

                CreateShiftInput(true)
            };

            uint sent =
                SendInput(
                    (uint)inputs.Length,
                    inputs,
                    Marshal.SizeOf<INPUT>());

            if (sent != inputs.Length)
                return false;

            _syntheticShiftDown = false;
        }
        else
        {
            if (!SendKey(
                    vk,
                    true))
            {
                return false;
            }
        }

        _shiftedKeys.Remove(vk);

        return true;
    }


    // ------------------------------------------------------------
    // SendInput helpers
    // ------------------------------------------------------------

    private static bool SendKey(
        int vk,
        bool keyUp)
    {
        INPUT[] inputs =
        {
            CreateKeyInput(
                vk,
                keyUp)
        };

        uint sent =
            SendInput(
                1,
                inputs,
                Marshal.SizeOf<INPUT>());

        return sent == 1;
    }

    private static INPUT CreateKeyInput(
        int virtualKey,
        bool keyUp)
    {
        return new INPUT
        {
            type = INPUT_KEYBOARD,

            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk =
                        (ushort)virtualKey,

                    wScan = 0,

                    dwFlags =
                        keyUp
                            ? KEYEVENTF_KEYUP
                            : 0,

                    time = 0,

                    dwExtraInfo =
                        InjectionMarker
                }
            }
        };
    }


    private static INPUT CreateShiftInput(
        bool keyUp)
    {
        return new INPUT
        {
            type = INPUT_KEYBOARD,

            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = 0,

                    wScan = SCAN_LSHIFT,

                    dwFlags =
                        KEYEVENTF_SCANCODE |
                        (keyUp
                            ? KEYEVENTF_KEYUP
                            : 0),

                    time = 0,

                    dwExtraInfo =
                        InjectionMarker
                }
            }
        };
    }


    // ------------------------------------------------------------
    // Force CapsLock OFF
    // ------------------------------------------------------------

    private static void ForceCapsLockOff()
    {
        bool capsIsOn =
            (GetKeyState(VK_CAPITAL) & 1) != 0;

        if (!capsIsOn)
            return;

        INPUT[] inputs =
        {
            CreateKeyInput(
                VK_CAPITAL,
                false),

            CreateKeyInput(
                VK_CAPITAL,
                true)
        };

        SendInput(
            (uint)inputs.Length,
            inputs,
            Marshal.SizeOf<INPUT>());
    }


    // ------------------------------------------------------------
    // Dispose
    // ------------------------------------------------------------

    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();

        _capsHoldTimer.Dispose();

        _disposed = true;

        GC.SuppressFinalize(this);
    }
}