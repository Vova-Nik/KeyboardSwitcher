using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace KeyboardSwitcher;

public sealed class TextInputCollector
{
    private readonly TextInputTracker _tracker;
    private readonly KeyboardLayoutContext _layoutContext;

    public TextInputTracker Tracker => _tracker;

    public TextInputCollector(
        TextInputTracker tracker,
        KeyboardLayoutContext layoutContext)
    {
        _tracker = tracker;
        _layoutContext = layoutContext;
    }

    public void Process(KeyInfo info)
    {
        DebugLog.Write(
            $"COLLECTOR: " +
            $"Key={info.Key} " +
            $"SC=0x{info.ScanCode:X2} " +
            $"Shift={info.Shift} " +
            $"CapsHeld={info.CapsLockHeld} " +
            $"Converter={info.IsConverterInput} " +
            $"BufferBefore={_tracker.Count}");

        // --------------------------------------------------------
        // Наші синтетичні події не збираємо.
        // --------------------------------------------------------

        if (info.IsConverterInput)
            return;

        // --------------------------------------------------------
        // Коректуючі клавіші очищають ActiveBuffer.
        // --------------------------------------------------------

        if (IsClearKey(info))
        {
            _tracker.Clear();

            DebugLog.Write(
                $"COLLECTOR: ClearKey " +
                $"Key={info.Key} " +
                $"BufferAfter={_tracker.Count}");

            return;
        }

        // --------------------------------------------------------
        // Модифікатори самі по собі не є текстом.
        // --------------------------------------------------------

        if (IsModifier(info.Key))
            return;

        if (info.Ctrl || info.Alt)
            return;

        if (!IsTextKey(info.Key))
            return;

        // --------------------------------------------------------
        // Після явного перемикання розкладки перше слово
        // не потрапляє до наших буферів.
        //
        // Пропускаємо всі клавіші до Space / comma / period
        // та інших розділових знаків.
        // --------------------------------------------------------

        if (_tracker.IsIgnoringNextWord)
        {
            if (IsWordTerminator(info.Key))
            {
                _tracker.FinishIgnoredWord();

                DebugLog.Write(
                    $"COLLECTOR: Ignored word finished " +
                    $"by {info.Key}. " +
                    $"Buffering resumed.");
            }
            else
            {
                DebugLog.Write(
                    $"COLLECTOR: Ignoring word. " +
                    $"Key={info.Key}");
            }

            return;
        }

        // --------------------------------------------------------
        // Збираємо звичайний текст.
        // --------------------------------------------------------

        var keyStroke =
            new KeyStroke(
                info.ScanCode,
                info.Extended,
                info.Shift,
                info.Ctrl,
                info.Alt);

        IntPtr currentHkl =
            _layoutContext.GetCurrentHkl();

        IntPtr hwnd =
            GetForegroundWindow();

        _tracker.Add(
            keyStroke,
            currentHkl,
            hwnd);

        DebugLog.Write(
            $"COLLECTOR: Added " +
            $"Key={info.Key} " +
            $"BufferAfter={_tracker.Count} " +
            $"SourceHkl=0x" +
            $"{_tracker.SourceHkl.ToInt64():X} " +
            $"Hwnd=0x{hwnd.ToInt64():X}");
    }

    private static bool IsClearKey(
        KeyInfo info)
    {
        return info.Key == Keys.Back ||
               info.Key == Keys.Delete ||
               info.Key == Keys.Left ||
               info.Key == Keys.Right ||
               info.Key == Keys.Up ||
               info.Key == Keys.Down;
    }

    private static bool IsModifier(
        Keys key)
    {
        return key == Keys.ShiftKey ||
               key == Keys.LShiftKey ||
               key == Keys.RShiftKey ||
               key == Keys.ControlKey ||
               key == Keys.LControlKey ||
               key == Keys.RControlKey ||
               key == Keys.Menu ||
               key == Keys.LMenu ||
               key == Keys.RMenu;
    }

    private static bool IsWordTerminator(
        Keys key)
    {
        return key == Keys.Space ||
               key == Keys.Oemcomma ||
               key == Keys.OemPeriod ||
               key == Keys.OemSemicolon ||
               key == Keys.OemQuotes ||
               key == Keys.OemQuestion;
    }

    private static bool IsTextKey(
        Keys key)
    {
        if (key >= Keys.A &&
            key <= Keys.Z)
        {
            return true;
        }

        if (key >= Keys.D0 &&
            key <= Keys.D9)
        {
            return true;
        }

        if (key >= Keys.NumPad0 &&
            key <= Keys.NumPad9)
        {
            return true;
        }

        switch (key)
        {
            case Keys.OemMinus:
            case Keys.Oemplus:
            case Keys.OemOpenBrackets:
            case Keys.OemCloseBrackets:
            case Keys.OemSemicolon:
            case Keys.OemQuotes:
            case Keys.Oemcomma:
            case Keys.OemPeriod:
            case Keys.OemQuestion:
            case Keys.OemBackslash:
            case Keys.Oemtilde:
            case Keys.Space:
                return true;

            default:
                return false;
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
}