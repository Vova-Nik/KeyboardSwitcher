using System.Text;

namespace KeyboardSwitcher;

public sealed class TextLayoutConverter
{
    private readonly KeyboardLayoutConverter _keyboardLayoutConverter;

    public TextLayoutConverter(
        KeyboardLayoutConverter keyboardLayoutConverter)
    {
        _keyboardLayoutConverter =
            keyboardLayoutConverter;
    }

    public string Convert(
        IReadOnlyList<KeyStroke> strokes,
        TestKeyboardLayout sourceLayout,
        TestKeyboardLayout targetLayout)
    {
        var result =
            new StringBuilder();

        foreach (KeyStroke stroke in strokes)
        {
            string sourceChar =
                _keyboardLayoutConverter.ConvertScanCode(
                    stroke.ScanCode,
                    stroke.Shift,
                    sourceLayout);

            if (string.IsNullOrEmpty(sourceChar))
                continue;

            string targetChar =
                _keyboardLayoutConverter.ConvertScanCode(
                    stroke.ScanCode,
                    stroke.Shift,
                    targetLayout);

            if (string.IsNullOrEmpty(targetChar))
                continue;

            result.Append(targetChar);
        }

        return result.ToString();
    }
}