using System.Runtime.InteropServices;

namespace KeyboardSwitcher;

public sealed class LanguageSwitcher
{
    private const uint WM_INPUTLANGCHANGEREQUEST = 0x0050;


    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();


    [DllImport("user32.dll")]
    private static extern IntPtr PostMessage(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam);


    private readonly KeyboardLayouts _layouts;


    public LanguageSwitcher(
        KeyboardLayouts layouts)
    {
        _layouts = layouts;
    }


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


    private bool SwitchTo(
        KeyboardLayout? layout)
    {
        if (layout == null)
            return false;


        IntPtr window =
            GetForegroundWindow();


        if (window == IntPtr.Zero)
            return false;


        // --------------------------------------------------------
        // Просимо Windows переключити input language
        // активного вікна на конкретний HKL.
        // --------------------------------------------------------

        IntPtr result =
            PostMessage(
                window,
                WM_INPUTLANGCHANGEREQUEST,
                IntPtr.Zero,
                layout.Hkl);


        return result != IntPtr.Zero;
    }
}