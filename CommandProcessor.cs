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

        if (commandEvent.Action == CommandAction.Single)
        {
            ProcessSingle(
                commandEvent.Command);

            return;
        }

        if (commandEvent.Action == CommandAction.Double)
        {
            ProcessDouble(
                commandEvent.Command);

            return;
        }
    }

    private void ProcessSingle(
        LayoutCommand command)
    {
        if (_tracker.ActiveBuffer.IsEmpty)
        {
            DebugLog.Write(
                $"PROCESSOR: Single with empty ActiveBuffer. " +
                $"PendingCount={_tracker.PendingBuffer.Count}. " +
                $"Switching directly to {command}.");

            // Pending НЕ очищаємо.
            QueueLanguageSwitch(command);

            return;
        }

        _tracker.SaveActiveToPending();

        DebugLog.Write(
            $"PROCESSOR: Single handled. " +
            $"PendingCount={_tracker.PendingBuffer.Count} " +
            $"PendingHkl=0x{_tracker.PendingBuffer.SourceHkl.ToInt64():X} " +
            $"PendingHwnd=0x{_tracker.PendingBuffer.Hwnd.ToInt64():X}");

        QueueLanguageSwitch(command);
    }


    //private void ProcessDouble(
    //LayoutCommand command)
    //{
    //    DebugLog.Write(
    //        $"PROCESSOR: Double received. " +
    //        $"Command={command}");

    //    IntPtr currentHwnd =
    //        GetForegroundWindow();

    //    DebugLog.Write(
    //        $"PROCESSOR: Double snapshot. " +
    //        $"CurrentHwnd=0x{currentHwnd.ToInt64():X} " +
    //        $"PendingHwnd=0x{_tracker.PendingBuffer.Hwnd.ToInt64():X} " +
    //        $"PendingCount={_tracker.PendingBuffer.Count}");

    //    if (_tracker.PendingBuffer.IsEmpty)
    //    {
    //        DebugLog.Write(
    //            "PROCESSOR: Double ignored. " +
    //            "PendingBuffer is empty.");

    //        return;
    //    }

    //    if (currentHwnd == IntPtr.Zero)
    //    {
    //        DebugLog.Write(
    //            "PROCESSOR: Double ignored. " +
    //            "ForegroundWindow is zero.");

    //        return;
    //    }

    //    if (currentHwnd != _tracker.PendingBuffer.Hwnd)
    //    {
    //        DebugLog.Write(
    //            $"PROCESSOR: Double ignored. " +
    //            $"Window changed. " +
    //            $"PendingHwnd=0x" +
    //            $"{_tracker.PendingBuffer.Hwnd.ToInt64():X} " +
    //            $"CurrentHwnd=0x" +
    //            $"{currentHwnd.ToInt64():X}");

    //        return;
    //    }

    //    DebugLog.Write(
    //        $"PROCESSOR: Double accepted. " +
    //        $"Hwnd=0x{currentHwnd.ToInt64():X}");

    //    // Конвертацію поки НЕ запускаємо.
    //}

    private void ProcessDouble(LayoutCommand command)
    {
        DebugLog.Write(
            $"PROCESSOR: Double received. " +
            $"Command={command}");

        IntPtr currentHwnd = GetForegroundWindow();

        DebugLog.Write(
            $"PROCESSOR: Double snapshot. " +
            $"CurrentHwnd=0x{currentHwnd.ToInt64():X} " +
            $"PendingHwnd=0x" +
            $"{_tracker.PendingBuffer.Hwnd.ToInt64():X} " +
            $"PendingCount={_tracker.PendingBuffer.Count} " +
            $"ActiveCount={_tracker.ActiveBuffer.Count}");

        if (currentHwnd == IntPtr.Zero)
        {
            DebugLog.Write(
                "PROCESSOR: Double ignored. " +
                "ForegroundWindow is zero.");
            return;
        }

        // If there is new active text, it becomes
        // the new Pending text for conversion.
        if (!_tracker.ActiveBuffer.IsEmpty)
        {
            DebugLog.Write(
                $"PROCESSOR: Double has ActiveBuffer. " +
                $"Moving Active to Pending. " +
                $"ActiveCount={_tracker.ActiveBuffer.Count}");

            _tracker.SaveActiveToPending();

            DebugLog.Write(
                $"PROCESSOR: Active moved to Pending. " +
                $"PendingCount={_tracker.PendingBuffer.Count} " +
                $"PendingHwnd=0x" +
                $"{_tracker.PendingBuffer.Hwnd.ToInt64():X}");
        }

        if (_tracker.PendingBuffer.IsEmpty)
        {
            DebugLog.Write(
                "PROCESSOR: Double ignored. " +
                "PendingBuffer is empty.");
            return;
        }

        if (currentHwnd != _tracker.PendingBuffer.Hwnd)
        {
            DebugLog.Write(
                $"PROCESSOR: Double ignored. " +
                $"Window changed. " +
                $"PendingHwnd=0x" +
                $"{_tracker.PendingBuffer.Hwnd.ToInt64():X} " +
                $"CurrentHwnd=0x" +
                $"{currentHwnd.ToInt64():X}");
            return;
        }

        DebugLog.Write(
            $"PROCESSOR: Double accepted. " +
            $"Hwnd=0x{currentHwnd.ToInt64():X}");

        QueuePendingConversion(
            command,
            currentHwnd);
    }


    //private void QueuePendingConversion(
    //    LayoutCommand command)
    //{
    //    ThreadPool.QueueUserWorkItem(
    //        _ =>
    //        {
    //            DebugLog.Write(
    //                $"PROCESSOR: Starting pending conversion. " +
    //                $"Command={command}");

    //            ConvertPending(command);

    //            DebugLog.Write(
    //                $"PROCESSOR: Pending conversion finished. " +
    //                $"Command={command}");
    //        });
    //}

    private void QueuePendingConversion(
    LayoutCommand command,
    IntPtr targetHwnd)
    {
        ThreadPool.QueueUserWorkItem(
            _ =>
            {
                DebugLog.Write(
                    $"PROCESSOR: Starting pending conversion. " +
                    $"Command={command} " +
                    $"TargetHwnd=0x{targetHwnd.ToInt64():X}");

                ConvertPending(
                    command,
                    targetHwnd);

                DebugLog.Write(
                    $"PROCESSOR: Pending conversion finished. " +
                    $"Command={command} " +
                    $"TargetHwnd=0x{targetHwnd.ToInt64():X}");
            });
    }
    private void ConvertPending(
        LayoutCommand command,
        IntPtr targetHwnd)
    {
        IntPtr sourceHkl =
            _tracker.PendingBuffer.SourceHkl;

        DebugLog.Write(
            $"PROCESSOR: ConvertPending started. " +
            $"TargetHwnd=0x{targetHwnd.ToInt64():X} " +
            $"CurrentHwnd=0x" +
            $"{GetForegroundWindow().ToInt64():X}");

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

        if (string.IsNullOrEmpty(result))
        {
            DebugLog.Write(
                "PROCESSOR: Conversion produced empty result.");

            return;
        }

        var textReplacer =
            new TextReplacer();

        int count =
            _tracker.PendingBuffer.Count;

        bool deleted =
            textReplacer.DeleteCharacters(count);

        DebugLog.Write(
            $"PROCESSOR: DELETE RESULT = {deleted}");

        if (!deleted)
        {
            DebugLog.Write(
                "PROCESSOR: INSERT skipped because DELETE failed.");

            return;
        }

        bool inserted =
            textReplacer.InsertText(result);

        DebugLog.Write(
            $"PROCESSOR: INSERT RESULT = {inserted}");

        if (inserted)
        {
            DebugLog.Write(
                $"PROCESSOR: PendingBuffer preserved. " +
                $"Count={_tracker.PendingBuffer.Count}");
        }
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

//hello.це нормальною привітю ХЗ як його тестувати