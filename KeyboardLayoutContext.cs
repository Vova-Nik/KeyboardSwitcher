
using System.Runtime.InteropServices;

namespace KeyboardSwitcher;

public sealed class KeyboardLayoutContext
{
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
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(
        IntPtr hWnd,
        out uint processId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetGUIThreadInfo(
        uint idThread,
        ref GUITHREADINFO lpguithreadinfo);

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(
        uint idThread);

    public string GetDiagnostic()
    {
        IntPtr foregroundWindow =
            GetForegroundWindow();

        if (foregroundWindow == IntPtr.Zero)
        {
            return "ForegroundWindow = 0";
        }

        uint foregroundThreadId =
            GetWindowThreadProcessId(
                foregroundWindow,
                out _);

        if (foregroundThreadId == 0)
        {
            return
                $"ForegroundWindow = 0x{foregroundWindow.ToInt64():X}\r\n" +
                "ForegroundThreadId = 0";
        }

        var guiInfo = new GUITHREADINFO
        {
            cbSize = (uint)Marshal.SizeOf<GUITHREADINFO>()
        };

        bool guiResult =
            GetGUIThreadInfo(
                foregroundThreadId,
                ref guiInfo);

        if (!guiResult)
        {
            return
                $"ForegroundWindow = 0x{foregroundWindow.ToInt64():X}\r\n" +
                $"ForegroundThreadId = {foregroundThreadId}\r\n" +
                $"GetGUIThreadInfo = FALSE\r\n" +
                $"LastError = {Marshal.GetLastWin32Error()}";
        }

        IntPtr focusWindow =
            guiInfo.hwndFocus != IntPtr.Zero
                ? guiInfo.hwndFocus
                : guiInfo.hwndActive;

        if (focusWindow == IntPtr.Zero)
        {
            return
                $"ForegroundWindow = 0x{foregroundWindow.ToInt64():X}\r\n" +
                $"ForegroundThreadId = {foregroundThreadId}\r\n" +
                $"hwndActive = 0x{guiInfo.hwndActive.ToInt64():X}\r\n" +
                $"hwndFocus = 0x{guiInfo.hwndFocus.ToInt64():X}\r\n" +
                "FocusWindow = 0";
        }

        uint focusThreadId =
            GetWindowThreadProcessId(
                focusWindow,
                out _);

        if (focusThreadId == 0)
        {
            return
                $"ForegroundWindow = 0x{foregroundWindow.ToInt64():X}\r\n" +
                $"ForegroundThreadId = {foregroundThreadId}\r\n" +
                $"hwndActive = 0x{guiInfo.hwndActive.ToInt64():X}\r\n" +
                $"hwndFocus = 0x{guiInfo.hwndFocus.ToInt64():X}\r\n" +
                $"FocusWindow = 0x{focusWindow.ToInt64():X}\r\n" +
                "FocusThreadId = 0";
        }

        IntPtr hkl =
            GetKeyboardLayout(focusThreadId);

        return
            $"ForegroundWindow = 0x{foregroundWindow.ToInt64():X}\r\n" +
            $"ForegroundThreadId = {foregroundThreadId}\r\n" +
            $"hwndActive = 0x{guiInfo.hwndActive.ToInt64():X}\r\n" +
            $"hwndFocus = 0x{guiInfo.hwndFocus.ToInt64():X}\r\n" +
            $"FocusWindow = 0x{focusWindow.ToInt64():X}\r\n" +
            $"FocusThreadId = {focusThreadId}\r\n" +
            $"HKL = 0x{hkl.ToInt64():X}";
    }

    /*public IntPtr GetCurrentHkl()
    {
        IntPtr foregroundWindow =
            GetForegroundWindow();

        if (foregroundWindow == IntPtr.Zero)
            return IntPtr.Zero;

        uint foregroundThreadId =
            GetWindowThreadProcessId(
                foregroundWindow,
                out _);

        if (foregroundThreadId == 0)
            return IntPtr.Zero;

        var guiInfo = new GUITHREADINFO
        {
            cbSize = (uint)Marshal.SizeOf<GUITHREADINFO>()
        };

        if (!GetGUIThreadInfo(
                foregroundThreadId,
                ref guiInfo))
        {
            return IntPtr.Zero;
        }

        IntPtr focusWindow =
            guiInfo.hwndFocus != IntPtr.Zero
                ? guiInfo.hwndFocus
                : guiInfo.hwndActive;

        if (focusWindow == IntPtr.Zero)
            return IntPtr.Zero;

        uint focusThreadId =
            GetWindowThreadProcessId(
                focusWindow,
                out _);

        if (focusThreadId == 0)
            return IntPtr.Zero;

        return GetKeyboardLayout(focusThreadId);
    }*/
    /*public IntPtr GetCurrentHkl()
    {
        IntPtr foregroundWindow =
            GetForegroundWindow();

        DebugLog.Write(
            $"GetCurrentHkl: ForegroundWindow=0x{foregroundWindow.ToInt64():X}");

        if (foregroundWindow == IntPtr.Zero)
        {
            DebugLog.Write(
                "GetCurrentHkl: ForegroundWindow == 0");

            return IntPtr.Zero;
        }

        uint foregroundThreadId =
            GetWindowThreadProcessId(
                foregroundWindow,
                out uint foregroundProcessId);

        DebugLog.Write(
            $"GetCurrentHkl: " +
            $"ForegroundThreadId={foregroundThreadId}, " +
            $"ProcessId={foregroundProcessId}");

        if (foregroundThreadId == 0)
        {
            DebugLog.Write(
                "GetCurrentHkl: ForegroundThreadId == 0");

            return IntPtr.Zero;
        }

        var guiInfo = new GUITHREADINFO
        {
            cbSize = (uint)Marshal.SizeOf<GUITHREADINFO>()
        };

        bool result =
            GetGUIThreadInfo(
                foregroundThreadId,
                ref guiInfo);

        DebugLog.Write(
            $"GetCurrentHkl: " +
            $"GetGUIThreadInfo={result}, " +
            $"LastError={Marshal.GetLastWin32Error()}");

        DebugLog.Write(
            $"GetCurrentHkl: " +
            $"hwndActive=0x{guiInfo.hwndActive.ToInt64():X}, " +
            $"hwndFocus=0x{guiInfo.hwndFocus.ToInt64():X}");

        if (!result)
            return IntPtr.Zero;

        IntPtr focusWindow =
            guiInfo.hwndFocus != IntPtr.Zero
                ? guiInfo.hwndFocus
                : guiInfo.hwndActive;

        DebugLog.Write(
            $"GetCurrentHkl: " +
            $"SelectedWindow=0x{focusWindow.ToInt64():X}");

        if (focusWindow == IntPtr.Zero)
            return IntPtr.Zero;

        uint focusThreadId =
            GetWindowThreadProcessId(
                focusWindow,
                out uint focusProcessId);

        DebugLog.Write(
            $"GetCurrentHkl: " +
            $"FocusThreadId={focusThreadId}, " +
            $"ProcessId={focusProcessId}");

        if (focusThreadId == 0)
            return IntPtr.Zero;

        IntPtr hkl =
            GetKeyboardLayout(focusThreadId);

        DebugLog.Write(
            $"GetCurrentHkl: " +
            $"HKL=0x{hkl.ToInt64():X}");

        return hkl;
    }*/

    public IntPtr GetCurrentHkl()
    {
        IntPtr foregroundWindow =
            GetForegroundWindow();

        DebugLog.Write(
            $"GetCurrentHkl: ForegroundWindow=0x{foregroundWindow.ToInt64():X}");

        if (foregroundWindow == IntPtr.Zero)
            return IntPtr.Zero;

        uint threadId =
            GetWindowThreadProcessId(
                foregroundWindow,
                out uint processId);

        DebugLog.Write(
            $"GetCurrentHkl: " +
            $"ThreadId={threadId}, " +
            $"ProcessId={processId}");

        if (threadId == 0)
            return IntPtr.Zero;

        IntPtr hkl =
            GetKeyboardLayout(threadId);

        DebugLog.Write(
            $"GetCurrentHkl: " +
            $"HKL=0x{hkl.ToInt64():X}");

        return hkl;
    }
}