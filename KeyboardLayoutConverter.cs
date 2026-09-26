using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace KeyboardSwitcher;

public sealed class TestKeyboardLayout
{
    public IntPtr Hkl { get; }
    public string LocaleName { get; }
    public string DisplayName { get; }

    public TestKeyboardLayout(
        IntPtr hkl,
        string localeName,
        string displayName)
    {
        Hkl = hkl;
        LocaleName = localeName;
        DisplayName = displayName;
    }

    public override string ToString()
    {
        return $"{DisplayName} [{LocaleName}] " +
               $"HKL = 0x{Hkl.ToInt64():X}";
    }
}

public sealed class KeyboardLayoutConverter
{
    private const uint MAPVK_VSC_TO_VK_EX = 3;

    private const uint TO_UNICODE_NO_STATE_CHANGE = 4;

    private const int VK_SHIFT = 0x10;
    private const uint KLF_SETFORPROCESS = 0x00000100;

    [DllImport("user32.dll")]
    private static extern int GetKeyboardLayoutList(
        int nBuff,
        [Out] IntPtr[] lpList);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKeyEx(
        uint uCode,
        uint uMapType,
        IntPtr dwhkl);

    //[DllImport("user32.dll")]
    //private static extern int ToUnicodeEx(
    //    uint wVirtKey,
    //    uint wScanCode,
    //    byte[] lpKeyState,
    //    [Out] char[] pwszBuff,
    //    int cchBuff,
    //    uint wFlags,
    //    IntPtr dwhkl);

    [DllImport(
    "user32.dll",
    CharSet = CharSet.Unicode,
    ExactSpelling = true)]
    private static extern int ToUnicodeEx(
    uint wVirtKey,
    uint wScanCode,
    byte[] lpKeyState,
    [Out, MarshalAs(UnmanagedType.LPWStr)]
    StringBuilder pwszBuff,
    int cchBuff,
    uint wFlags,
    IntPtr dwhkl);

    //[DllImport("user32.dll")]
    //private static extern bool GetKeyboardState(
    //byte[] lpKeyState);
    /// <summary>
    /// //////////////////////////////////////////////////////////////////////////////////////////

    [DllImport("user32.dll")]
    private static extern IntPtr ActivateKeyboardLayout(
    IntPtr hkl,
    uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(
        uint idThread);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
    ////////////////////////////////////////////////////////////

    public string TestActivateLayout(
     TestKeyboardLayout layout)
    {
        IntPtr before =
            GetKeyboardLayout(
                GetCurrentThreadId());

        IntPtr previous =
            ActivateKeyboardLayout(
                layout.Hkl,
                KLF_SETFORPROCESS);

        IntPtr after =
            GetKeyboardLayout(
                GetCurrentThreadId());

        return
            $"Before = 0x{before.ToInt64():X}\r\n" +
            $"Previous = 0x{previous.ToInt64():X}\r\n" +
            $"Requested = 0x{layout.Hkl.ToInt64():X}\r\n" +
            $"After = 0x{after.ToInt64():X}";
    }
    /// <summary>
    /// ///////////////////////////////////////////////////////////
    /// </summary>
    /// <returns></returns>
    public IReadOnlyList<TestKeyboardLayout> GetLayouts()
    {
        int count =
            GetKeyboardLayoutList(
                0,
                Array.Empty<IntPtr>());

        if (count <= 0)
            return Array.Empty<TestKeyboardLayout>();

        var hkls =
            new IntPtr[count];

        int actualCount =
            GetKeyboardLayoutList(
                count,
                hkls);

        var result =
            new List<TestKeyboardLayout>();

        for (int i = 0; i < actualCount; i++)
        {
            IntPtr hkl = hkls[i];

            ushort langId =
                unchecked(
                    (ushort)hkl.ToInt64());

            try
            {
                CultureInfo culture =
                    CultureInfo.GetCultureInfo(
                        langId);

                result.Add(
                    new TestKeyboardLayout(
                        hkl,
                        culture.Name,
                        culture.DisplayName));
            }
            catch
            {
                // Unknown LANGID.
            }
        }

        return result;
    }

    //public string ConvertScanCode(
    //    uint scanCode,
    //    bool shift,
    //    TestKeyboardLayout layout)
    //{
    //    uint virtualKey =
    //        MapVirtualKeyEx(
    //            scanCode,
    //            MAPVK_VSC_TO_VK_EX,
    //            layout.Hkl);

    //    if (virtualKey == 0)
    //        return "";

    //    var keyboardState =
    //        new byte[256];

    //    if (shift)
    //        keyboardState[VK_SHIFT] = 0x80;

    //    var buffer =
    //        new char[8];

    //    int result =
    //        ToUnicodeEx(
    //            virtualKey,
    //            scanCode,
    //            keyboardState,
    //            buffer,
    //            buffer.Length,
    //            TO_UNICODE_NO_STATE_CHANGE,
    //            layout.Hkl);

    //    if (result == 0)
    //        return "";

    //    if (result < 0)
    //        return "[dead]";

    //    return new string(
    //        buffer,
    //        0,
    //        result);
    //}
    //public string ConvertScanCode(
    //    uint scanCode,
    //    bool shift,
    //    TestKeyboardLayout layout)
    //{
    //    uint virtualKey =
    //        MapVirtualKeyEx(
    //            scanCode,
    //            MAPVK_VSC_TO_VK_EX,
    //            layout.Hkl);

    //    if (virtualKey == 0)
    //        return "";

    //    var keyboardState =
    //        new byte[256];

    //    if (shift)
    //        keyboardState[VK_SHIFT] = 0x80;

    //    //var buffer =
    //    //    new char[8];
    //    var buffer =
    //        new StringBuilder(8);

    //    int result =
    //        ToUnicodeEx(
    //            virtualKey,
    //            0,
    //            keyboardState,
    //            buffer,
    //            buffer.Length,
    //            TO_UNICODE_NO_STATE_CHANGE,
    //            layout.Hkl);

    //    if (result == 0)
    //        return "";

    //    if (result < 0)
    //        return "[dead]";

    //    return new string(
    //        buffer,
    //        0,
    //        result);
    //}

    public string ConvertScanCode(
    uint scanCode,
    bool shift,
    TestKeyboardLayout layout)
    {
        uint virtualKey =
            MapVirtualKeyEx(
                scanCode,
                MAPVK_VSC_TO_VK_EX,
                layout.Hkl);

        if (virtualKey == 0)
            return "";

        var keyboardState =
            new byte[256];

        if (shift)
            keyboardState[VK_SHIFT] = 0x80;

        var buffer =
            new StringBuilder(8);

        int result =
            ToUnicodeEx(
                virtualKey,
                scanCode,
                keyboardState,
                buffer,
                buffer.Capacity,
                TO_UNICODE_NO_STATE_CHANGE,
                layout.Hkl);

        if (result == 0)
            return "";

        if (result < 0)
            return "[dead]";

        return buffer.ToString(
            0,
            result);
    }
}