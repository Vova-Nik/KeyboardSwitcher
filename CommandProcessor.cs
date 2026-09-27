using System.Runtime.InteropServices;

namespace KeyboardSwitcher;

public sealed class CommandProcessor
{
    private readonly TextInputTracker _tracker;
    private readonly LanguageSwitcher _languageSwitcher;
    private readonly KeyboardLayoutConverter _keyboardLayoutConverter;
    private readonly TextLayoutConverter _textLayoutConverter;

    public CommandProcessor(
        TextInputTracker tracker,
        LanguageSwitcher languageSwitcher,
        KeyboardLayoutConverter keyboardLayoutConverter,
        TextLayoutConverter textLayoutConverter)
    {
        _tracker = tracker;
        _languageSwitcher = languageSwitcher;
        _keyboardLayoutConverter = keyboardLayoutConverter;
        _textLayoutConverter = textLayoutConverter;
    }

    public void Process(CommandEvent commandEvent)
    {
        DebugLog.Write(
            $"PROCESSOR: " +
            $"Command={commandEvent.Command} " +
            $"Action={commandEvent.Action} " +
            $"ActiveCount={_tracker.ActiveBuffer.Count} " +
            $"PendingCount={_tracker.PendingBuffer.Count}");

        if (commandEvent.Action == CommandAction.First)
        {
            if (_tracker.ActiveBuffer.IsEmpty)
            {
                DebugLog.Write(
                    $"PROCESSOR: First with empty ActiveBuffer. " +
                    $"Switching directly to {commandEvent.Command}.");

                _tracker.ClearPending();

                QueueLanguageSwitch(
                    commandEvent.Command);

                return;
            }

            _tracker.SaveActiveToPending();

            DebugLog.Write(
                $"PROCESSOR: First handled. " +
                $"PendingCount={_tracker.PendingBuffer.Count} " +
                $"PendingHkl=0x{_tracker.PendingBuffer.SourceHkl.ToInt64():X} " +
                $"PendingHwnd=0x{_tracker.PendingBuffer.Hwnd.ToInt64():X}");

            QueueLanguageSwitch(
                commandEvent.Command);

            return;
        }

        if (commandEvent.Action != CommandAction.Second)
            return;

        if (_tracker.PendingBuffer.IsEmpty)
        {
            DebugLog.Write(
                "PROCESSOR: Second ignored. " +
                "PendingBuffer is empty.");

            return;
        }

        IntPtr currentHwnd =
            GetForegroundWindow();

        if (currentHwnd == IntPtr.Zero)
        {
            DebugLog.Write(
                "PROCESSOR: Second ignored. " +
                "ForegroundWindow is zero.");

            return;
        }

        if (currentHwnd != _tracker.PendingBuffer.Hwnd)
        {
            DebugLog.Write(
                $"PROCESSOR: Second ignored. " +
                $"Window changed. " +
                $"PendingHwnd=0x{_tracker.PendingBuffer.Hwnd.ToInt64():X} " +
                $"CurrentHwnd=0x{currentHwnd.ToInt64():X}");

            return;
        }

        DebugLog.Write(
            $"PROCESSOR: Second accepted. " +
            $"PendingHwnd=0x{_tracker.PendingBuffer.Hwnd.ToInt64():X}");

        ConvertPending(
            commandEvent.Command);
    }

    private void ConvertPending(
        LayoutCommand command)
    {
        IntPtr sourceHkl =
            _tracker.PendingBuffer.SourceHkl;

        if (sourceHkl == IntPtr.Zero)
        {
            DebugLog.Write(
                "PROCESSOR: Conversion failed. " +
                "Pending SourceHkl is zero.");

            return;
        }

        IReadOnlyList<TestKeyboardLayout> layouts =
            _keyboardLayoutConverter.GetLayouts();

        TestKeyboardLayout? sourceLayout =
            layouts.FirstOrDefault(
                layout => layout.Hkl == sourceHkl);

        if (sourceLayout == null)
        {
            DebugLog.Write(
                $"PROCESSOR: Conversion failed. " +
                $"Source layout not found. " +
                $"HKL=0x{sourceHkl.ToInt64():X}");

            return;
        }

        TestKeyboardLayout? targetLayout =
            GetTargetLayout(
                layouts,
                command);

        if (targetLayout == null)
        {
            DebugLog.Write(
                $"PROCESSOR: Conversion failed. " +
                $"Target layout not found. " +
                $"Command={command}");

            return;
        }

        DebugLog.Write(
            $"PROCESSOR: Converting Pending. " +
            $"Source={sourceLayout} " +
            $"Target={targetLayout}");

        string result =
            _textLayoutConverter.Convert(
                _tracker.PendingBuffer.GetBuffer(),
                sourceLayout,
                targetLayout);

        DebugLog.Write(
            $"PROCESSOR: CONVERSION RESULT = \"{result}\"");
    }

    private static TestKeyboardLayout? GetTargetLayout(
        IReadOnlyList<TestKeyboardLayout> layouts,
        LayoutCommand command)
    {
        string prefix =
            command switch
            {
                LayoutCommand.English => "en-",
                LayoutCommand.Russian => "ru-",
                LayoutCommand.Ukrainian => "uk-",
                _ => ""
            };

        if (string.IsNullOrEmpty(prefix))
            return null;

        return layouts.FirstOrDefault(
            layout =>
                layout.LocaleName.StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase));
    }

    private void QueueLanguageSwitch(
        LayoutCommand command)
    {
        ThreadPool.QueueUserWorkItem(
            _ =>
            {
                DebugLog.Write(
                    $"PROCESSOR: Starting language switch. " +
                    $"Command={command}");

                bool switched =
                    SwitchToCommandLayout(command);

                DebugLog.Write(
                    $"PROCESSOR: Switch result = {switched}");
            });
    }

    private bool SwitchToCommandLayout(
        LayoutCommand command)
    {
        return command switch
        {
            LayoutCommand.English =>
                _languageSwitcher.SwitchToEnglish(),

            LayoutCommand.Russian =>
                _languageSwitcher.SwitchToRussian(),

            LayoutCommand.Ukrainian =>
                _languageSwitcher.SwitchToUkrainian(),

            _ => false
        };
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
}