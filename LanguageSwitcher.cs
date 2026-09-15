using System.Runtime.InteropServices;

namespace KeyboardSwitcher;

public sealed class LanguageSwitcher
{
    private const uint WM_INPUTLANGCHANGEREQUEST = 0x0050;

    private const uint SMTO_ABORTIFHUNG = 0x0002;
    private const uint SMTO_BLOCK = 0x0001;

    private const uint SEND_MESSAGE_TIMEOUT = 1000;

    // ============================================================
    // Windows structures
    // ============================================================

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GUITHREADINFO
    {
        public uint cbSize;
        public uint flags;
        public IntPtr hwndActive;
        public IntPtr hwndFocus;
        public IntPtr hwndCapture;
        public IntPtr hwndMenuOwner;
        public IntPtr hwndMoveSize;
        public IntPtr hwndCaret;
        public RECT rcCaret;
    }

    // ============================================================
    // Windows API
    // ============================================================

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(
        IntPtr hWnd,
        IntPtr lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(
        uint idThread);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetGUIThreadInfo(
        uint idThread,
        ref GUITHREADINFO lpgui);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint Msg,
        IntPtr wParam,
        IntPtr lParam,
        uint fuFlags,
        uint uTimeout,
        out IntPtr lpdwResult);

    // ============================================================
    // Fields
    // ============================================================

    private readonly KeyboardLayouts _layouts;

    // ============================================================
    // Constructor
    // ============================================================

    public LanguageSwitcher(
        KeyboardLayouts layouts)
    {
        _layouts = layouts;
    }

    // ============================================================
    // Current layout
    // ============================================================

    public KeyboardLayout? CurrentLayout
    {
        get
        {
            IntPtr window =
                GetForegroundWindow();

            if (window == IntPtr.Zero)
                return null;

            uint threadId =
                GetWindowThreadProcessId(
                    window,
                    IntPtr.Zero);

            if (threadId == 0)
                return null;

            IntPtr hkl =
                GetKeyboardLayout(threadId);

            foreach (KeyboardLayout layout in _layouts.All)
            {
                if (layout.Hkl == hkl)
                    return layout;
            }

            return null;
        }
    }

    // ============================================================
    // Public language switching
    // ============================================================

    public bool SwitchToEnglish()
    {
        return SwitchTo(
            _layouts.English);
    }

    public bool SwitchToRussian()
    {
        return SwitchTo(
            _layouts.Russian);
    }

    public bool SwitchToUkrainian()
    {
        return SwitchTo(
            _layouts.Ukrainian);
    }

    // ============================================================
    // Main switching
    // ============================================================

    private bool SwitchTo(
        KeyboardLayout? targetLayout)
    {
        if (targetLayout == null)
            return false;

        // Якщо потрібна розкладка вже активна —
        // нічого робити не потрібно.
        if (IsCurrentLayout(targetLayout))
            return true;

        IntPtr foregroundWindow =
            GetForegroundWindow();

        if (foregroundWindow == IntPtr.Zero)
            return false;

        uint threadId =
            GetWindowThreadProcessId(
                foregroundWindow,
                IntPtr.Zero);

        if (threadId == 0)
            return false;

        // Знаходимо реальне вікно, яке має keyboard focus.
        IntPtr targetWindow =
            GetFocusWindow(threadId);

        if (targetWindow == IntPtr.Zero)
            return false;

        // Синхронно відправляємо запит на зміну розкладки.
        IntPtr messageResult;

        IntPtr sendResult =
            SendMessageTimeout(
                targetWindow,
                WM_INPUTLANGCHANGEREQUEST,
                IntPtr.Zero,
                targetLayout.Hkl,
                SMTO_ABORTIFHUNG | SMTO_BLOCK,
                SEND_MESSAGE_TIMEOUT,
                out messageResult);

        if (sendResult == IntPtr.Zero)
            return false;

        // Перевіряємо фактичний HKL thread-а.
        IntPtr currentHkl =
            GetKeyboardLayout(threadId);

        return currentHkl == targetLayout.Hkl;
    }

    // ============================================================
    // Find window with keyboard focus
    // ============================================================

    private static IntPtr GetFocusWindow(
        uint threadId)
    {
        GUITHREADINFO info =
            new GUITHREADINFO();

        info.cbSize =
            (uint)Marshal.SizeOf<GUITHREADINFO>();

        if (!GetGUIThreadInfo(
                threadId,
                ref info))
        {
            return IntPtr.Zero;
        }

        // Звичайний випадок:
        // hwndFocus — вікно/контрол з keyboard focus.
        if (info.hwndFocus != IntPtr.Zero)
            return info.hwndFocus;

        // Якщо окремого hwndFocus немає,
        // використовуємо активне вікно.
        if (info.hwndActive != IntPtr.Zero)
            return info.hwndActive;

        return IntPtr.Zero;
    }

    // ============================================================
    // Current HKL
    // ============================================================

    private IntPtr GetCurrentHkl()
    {
        IntPtr window =
            GetForegroundWindow();

        if (window == IntPtr.Zero)
            return IntPtr.Zero;

        uint threadId =
            GetWindowThreadProcessId(
                window,
                IntPtr.Zero);

        if (threadId == 0)
            return IntPtr.Zero;

        return GetKeyboardLayout(
            threadId);
    }

    private bool IsCurrentLayout(
        KeyboardLayout layout)
    {
        IntPtr currentHkl =
            GetCurrentHkl();

        return currentHkl != IntPtr.Zero &&
               currentHkl == layout.Hkl;
    }
}