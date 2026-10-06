using System.Runtime.InteropServices;

namespace KeyboardSwitcher;

public sealed class MouseHook : IDisposable
{
    private const int WH_MOUSE_LL = 14;

    private const int WM_MOUSEMOVE = 0x0200;

    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_MBUTTONDOWN = 0x0207;

    private const int WM_XBUTTONDOWN = 0x020B;

    // LLMHF_INJECTED
    private const uint LLMHF_INJECTED = 0x00000001;

    private delegate IntPtr LowLevelMouseProc(
        int nCode,
        IntPtr wParam,
        IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public int ptX;
        public int ptY;
        public uint mouseData;
        public uint flags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(
        int idHook,
        LowLevelMouseProc lpfn,
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

    private readonly LowLevelMouseProc _proc;

    private IntPtr _hookHandle;

    private bool _disposed;

    public event Action? MouseActivity;

    public MouseHook()
    {
        _proc = HookCallback;

        _hookHandle =
            SetWindowsHookEx(
                WH_MOUSE_LL,
                _proc,
                IntPtr.Zero,
                0);

        if (_hookHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                "Не вдалося встановити mouse hook.");
        }
    }

    private IntPtr HookCallback(
        int nCode,
        IntPtr wParam,
        IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int message =
                wParam.ToInt32();

            MSLLHOOKSTRUCT data =
                Marshal.PtrToStructure<MSLLHOOKSTRUCT>(
                    lParam);

            // Ігноруємо штучні mouse events.
            if ((data.flags & LLMHF_INJECTED) == 0)
            {
                if (IsMouseActivity(message))
                {
                    MouseActivity?.Invoke();
                }
            }
        }

        return CallNextHookEx(
            _hookHandle,
            nCode,
            wParam,
            lParam);
    }

    private static bool IsMouseActivity(
        int message)
    {
        return message == WM_MOUSEMOVE ||
               message == WM_LBUTTONDOWN ||
               message == WM_RBUTTONDOWN ||
               message == WM_MBUTTONDOWN ||
               message == WM_XBUTTONDOWN;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_hookHandle != IntPtr.Zero)
        {
            UnhookWindowsHookEx(
                _hookHandle);

            _hookHandle = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }
}