namespace KeyboardSwitcher;

public static class InputInjection
{
    public static readonly UIntPtr ConverterMarker =
        new UIntPtr(0x4B53434F4E564552UL);
}
