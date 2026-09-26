namespace KeyboardSwitcher;

public sealed class KeyStroke
{
    public uint ScanCode { get; }

    public bool Extended { get; }

    public bool Shift { get; }

    public bool Ctrl { get; }

    public bool Alt { get; }

    public KeyStroke(
        uint scanCode,
        bool extended,
        bool shift,
        bool ctrl,
        bool alt)
    {
        ScanCode = scanCode;
        Extended = extended;
        Shift = shift;
        Ctrl = ctrl;
        Alt = alt;
    }
}