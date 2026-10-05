using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace KeyboardSwitcher;

public sealed class KeyInfo
{
    public Keys Key { get; }
    public uint VirtualKey { get; }
    public uint ScanCode { get; }
    public bool Extended { get; }
    public bool Shift { get; }
    public bool Ctrl { get; }
    public bool Alt { get; }
    public bool CapsLockHeld { get; }
    public bool IsConverterInput { get; }

    public KeyInfo(
        Keys key,
        uint virtualKey,
        uint scanCode,
        bool extended,
        bool shift,
        bool ctrl,
        bool alt,
        bool capsLockHeld,
        bool isConverterInput)
    {
        Key = key;
        VirtualKey = virtualKey;
        ScanCode = scanCode;
        Extended = extended;
        Shift = shift;
        Ctrl = ctrl;
        Alt = alt;
        CapsLockHeld = capsLockHeld;
        IsConverterInput = isConverterInput;
    }
}

public sealed class KeyboardHook : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;

    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;

    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    private const int VK_SHIFT = 0x10;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12;
    private const int VK_CAPITAL = 0x14;

    private const int LLKHF_EXTENDED = 0x01;
    private const uint LLKHF_INJECTED = 0x10;

    // ------------------------------------------------------------
    // CapsLock long-press hint
    // ------------------------------------------------------------

    private readonly System.Windows.Forms.Timer _capsHoldTimer;

    private bool _capsHelpShown;

    // ------------------------------------------------------------
    // Hook
    // ------------------------------------------------------------

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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(
        int idHook,
        LowLevelKeyboardProc lpfn,
        IntPtr hMod,
        uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(
        IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(
        IntPtr hhk,
        int nCode,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(
        int vKey);

    private readonly LowLevelKeyboardProc _proc;

    private IntPtr _hookHandle;

    private bool _capsLockHeld;

    // ------------------------------------------------------------
    // Events
    // ------------------------------------------------------------

    public event Func<KeyInfo, bool>? KeyPressed;
    public event Action<KeyInfo>? KeyReleased;

    // Показати підказку після 2 секунд утримання CapsLock.
    public event Action? ShortcutHintRequested;

    // CapsLock відпущений.
    public event Action? CapsLockReleased;

    // ------------------------------------------------------------
    // Constructor
    // ------------------------------------------------------------

    public KeyboardHook()
    {
        _capsHoldTimer =
            new System.Windows.Forms.Timer
            {
                Interval = 2000
            };

        _capsHoldTimer.Tick +=
            OnCapsHoldTimerTick;

        _proc = HookCallback;

        _hookHandle = SetWindowsHookEx(
            WH_KEYBOARD_LL,
            _proc,
            IntPtr.Zero,
            0);

        if (_hookHandle == IntPtr.Zero)
        {
            _capsHoldTimer.Dispose();

            throw new InvalidOperationException(
                "Не вдалося встановити keyboard hook.");
        }
    }

    // ------------------------------------------------------------
    // Hook callback
    // ------------------------------------------------------------

    private IntPtr HookCallback(
        int nCode,
        IntPtr wParam,
        IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int message =
                wParam.ToInt32();

            if (message == WM_KEYDOWN ||
                message == WM_SYSKEYDOWN)
            {
                bool handled =
                    HandleKeyDown(lParam);

                if (handled)
                    return (IntPtr)1;
            }

            else if (message == WM_KEYUP ||
                     message == WM_SYSKEYUP)
            {
                HandleKeyUp(lParam);

                KBDLLHOOKSTRUCT data =
                    Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(
                        lParam);

                if (data.vkCode == VK_CAPITAL)
                    return (IntPtr)1;
            }
        }

        return CallNextHookEx(
            _hookHandle,
            nCode,
            wParam,
            lParam);
    }

    // ------------------------------------------------------------
    // KeyDown
    // ------------------------------------------------------------

    private bool HandleKeyDown(
        IntPtr lParam)
    {
        KBDLLHOOKSTRUCT data =
            Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(
                lParam);

        bool isCapsLock =
            data.vkCode == VK_CAPITAL;

        if (isCapsLock)
        {
            // Не перезапускаємо timer на autorepeat CapsLock.
            if (!_capsLockHeld)
            {
                _capsLockHeld = true;

                _capsHelpShown = false;

                _capsHoldTimer.Stop();
                _capsHoldTimer.Start();
            }
        }
        else
        {
            // Будь-яка інша клавіша скасовує очікування
            // підказки після тривалого утримання CapsLock.
            if (_capsLockHeld)
            {
                _capsHoldTimer.Stop();
            }
        }

        bool extended =
            (data.flags & LLKHF_EXTENDED) != 0;

        bool shift =
            IsKeyDown(VK_SHIFT);

        bool ctrl =
            IsKeyDown(VK_CONTROL);

        bool alt =
            IsKeyDown(VK_MENU);

        bool isConverterInput =
            (data.flags & LLKHF_INJECTED) != 0 &&
            data.dwExtraInfo == InputInjection.ConverterMarker;

        var info = new KeyInfo(
            (Keys)data.vkCode,
            data.vkCode,
            data.scanCode,
            extended,
            shift,
            ctrl,
            alt,
            _capsLockHeld,
            isConverterInput);

        bool handled =
            KeyPressed?.Invoke(info) ?? false;

        DebugLog.Write(
            $"HOOK: KeyDown " +
            $"Key={info.Key} " +
            $"VK=0x{info.VirtualKey:X} " +
            $"SC=0x{info.ScanCode:X} " +
            $"CapsHeld={info.CapsLockHeld} " +
            $"Converter={info.IsConverterInput} " +
            $"Handled={handled}");

        return handled;
    }

    // ------------------------------------------------------------
    // KeyUp
    // ------------------------------------------------------------

    private void HandleKeyUp(
        IntPtr lParam)
    {
        KBDLLHOOKSTRUCT data =
            Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(
                lParam);

        bool isCapsLock =
            data.vkCode == VK_CAPITAL;

        bool extended =
            (data.flags & LLKHF_EXTENDED) != 0;

        bool shift =
            IsKeyDown(VK_SHIFT);

        bool ctrl =
            IsKeyDown(VK_CONTROL);

        bool alt =
            IsKeyDown(VK_MENU);

        bool isConverterInput =
            (data.flags & LLKHF_INJECTED) != 0 &&
            data.dwExtraInfo ==
                InputInjection.ConverterMarker;

        var info = new KeyInfo(
            (Keys)data.vkCode,
            data.vkCode,
            data.scanCode,
            extended,
            shift,
            ctrl,
            alt,
            _capsLockHeld,
            isConverterInput);

        KeyReleased?.Invoke(info);

        if (isCapsLock)
        {
            _capsHoldTimer.Stop();

            _capsHelpShown = false;

            _capsLockHeld = false;

            CapsLockReleased?.Invoke();
        }
    }

    // ------------------------------------------------------------
    // CapsLock hold timer
    // ------------------------------------------------------------

    private void OnCapsHoldTimerTick(
        object? sender,
        EventArgs e)
    {
        _capsHoldTimer.Stop();

        if (!_capsLockHeld)
            return;

        if (_capsHelpShown)
            return;

        _capsHelpShown = true;

        ShortcutHintRequested?.Invoke();
    }

    // ------------------------------------------------------------
    // Keyboard state
    // ------------------------------------------------------------

    private static bool IsKeyDown(
        int virtualKey)
    {
        return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
    }

    // ------------------------------------------------------------
    // Dispose
    // ------------------------------------------------------------

    public void Dispose()
    {
        _capsHoldTimer.Stop();
        _capsHoldTimer.Dispose();

        if (_hookHandle != IntPtr.Zero)
        {
            UnhookWindowsHookEx(
                _hookHandle);

            _hookHandle = IntPtr.Zero;
        }

        _capsLockHeld = false;
    }
}