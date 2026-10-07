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

    // ============================================================
    // Command processing
    // ============================================================

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
            ProcessSingle(commandEvent.Command);
            return;
        }

        if (commandEvent.Action == CommandAction.Double)
        {
            ProcessDouble(commandEvent.Command);
            return;
        }
    }

    // ============================================================
    // Single command
    // ============================================================

    private void ProcessSingle(
        LayoutCommand command)
    {
        if (_tracker.ActiveBuffer.IsEmpty)
        {
            DebugLog.Write(
                $"PROCESSOR: Single with empty ActiveBuffer. " +
                $"PendingCount={_tracker.PendingBuffer.Count}. " +
                $"Switching directly to {command}.");

            QueueLanguageSwitch(
                command,
                ignoreNextWord: true);

            return;
        }

        _tracker.SaveActiveToPending();

        DebugLog.Write(
            $"PROCESSOR: Single handled. " +
            $"PendingCount={_tracker.PendingBuffer.Count} " +
            $"PendingHkl=0x" +
            $"{_tracker.PendingBuffer.SourceHkl.ToInt64():X} " +
            $"PendingHwnd=0x" +
            $"{_tracker.PendingBuffer.Hwnd.ToInt64():X}");

        QueueLanguageSwitch(
            command,
            ignoreNextWord: true);
    }

    // ============================================================
    // Double command
    // ============================================================

    private void ProcessDouble(
        LayoutCommand command)
    {
        DebugLog.Write(
            $"PROCESSOR: Double received. " +
            $"Command={command}");

        IntPtr currentHwnd =
            GetForegroundWindow();

        DebugLog.Write(
            $"PROCESSOR: Double snapshot. " +
            $"CurrentHwnd=0x" +
            $"{currentHwnd.ToInt64():X} " +
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

        // --------------------------------------------------------
        // Якщо є новий Active текст —
        // він стає новим Pending для конвертації.
        // --------------------------------------------------------

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

        if (currentHwnd !=
            _tracker.PendingBuffer.Hwnd)
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

    // ============================================================
    // Pending conversion
    // ============================================================

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
                    $"TargetHwnd=0x" +
                    $"{targetHwnd.ToInt64():X}");

                ConvertPending(
                    command,
                    targetHwnd);

                DebugLog.Write(
                    $"PROCESSOR: Pending conversion finished. " +
                    $"Command={command} " +
                    $"TargetHwnd=0x" +
                    $"{targetHwnd.ToInt64():X}");
            });
    }

    private void ConvertPending(
        LayoutCommand command,
        IntPtr targetHwnd)
    {
        DebugLog.Write(
            $"PROCESSOR: ConvertPending started. " +
            $"TargetHwnd=0x" +
            $"{targetHwnd.ToInt64():X} " +
            $"CurrentHwnd=0x" +
            $"{GetForegroundWindow().ToInt64():X}");

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
                layout =>
                    layout.Hkl == sourceHkl);

        if (sourceLayout == null)
        {
            DebugLog.Write(
                $"PROCESSOR: Conversion failed. " +
                $"Source layout not found. " +
                $"HKL=0x" +
                $"{sourceHkl.ToInt64():X}");

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
            textReplacer.DeleteCharacters(
                count);

        DebugLog.Write(
            $"PROCESSOR: DELETE RESULT = {deleted}");

        if (!deleted)
        {
            DebugLog.Write(
                "PROCESSOR: INSERT skipped because DELETE failed.");

            return;
        }

        bool inserted =
            textReplacer.InsertText(
                result);

        DebugLog.Write(
            $"PROCESSOR: INSERT RESULT = {inserted}");

        if (inserted)
        {
            DebugLog.Write(
                $"PROCESSOR: PendingBuffer preserved. " +
                $"Count={_tracker.PendingBuffer.Count}");

            // Після конвертації встановлюємо цільову розкладку.
            // Але наступне слово НЕ пропускаємо.
            QueueLanguageSwitch(
                command,
                ignoreNextWord: false);
        }
    }

    // ============================================================
    // Target layout
    // ============================================================

    private static TestKeyboardLayout? GetTargetLayout(
        IReadOnlyList<TestKeyboardLayout> layouts,
        LayoutCommand command)
    {
        string prefix =
            command switch
            {
                LayoutCommand.English =>
                    "en-",

                LayoutCommand.Russian =>
                    "ru-",

                LayoutCommand.Ukrainian =>
                    "uk-",

                _ =>
                    ""
            };

        if (string.IsNullOrEmpty(prefix))
            return null;

        return layouts.FirstOrDefault(
            layout =>
                layout.LocaleName.StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase));
    }

    // ============================================================
    // Language switching
    // ============================================================

    private void QueueLanguageSwitch(
        LayoutCommand command,
        bool ignoreNextWord)
    {
        ThreadPool.QueueUserWorkItem(
            _ =>
            {
                DebugLog.Write(
                    $"PROCESSOR: Starting language switch. " +
                    $"Command={command}");

                KeyboardLayout? layoutBefore =
                    _languageSwitcher.CurrentLayout;

                IntPtr hklBefore =
                    layoutBefore?.Hkl ??
                    IntPtr.Zero;

                bool switched =
                    SwitchToCommandLayout(
                        command);

                KeyboardLayout? layoutAfter =
                    _languageSwitcher.CurrentLayout;

                IntPtr hklAfter =
                    layoutAfter?.Hkl ??
                    IntPtr.Zero;

                DebugLog.Write(
                    $"PROCESSOR: Switch result = {switched} " +
                    $"HKL before=0x" +
                    $"{hklBefore.ToInt64():X} " +
                    $"after=0x" +
                    $"{hklAfter.ToInt64():X}");

                // ------------------------------------------------
                // Пропускаємо наступне слово тільки для Single,
                // якщо ми дійсно змінили HKL.
                // ------------------------------------------------

                if (ignoreNextWord &&
                    switched &&
                    hklBefore != IntPtr.Zero &&
                    hklAfter != IntPtr.Zero &&
                    hklBefore != hklAfter)
                {
                    _tracker.BeginIgnoreNextWord();

                    DebugLog.Write(
                        "PROCESSOR: " +
                        "Next word will be ignored.");
                }
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

            _ =>
                false
        };
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
}