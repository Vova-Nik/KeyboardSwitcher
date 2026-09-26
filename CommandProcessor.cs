namespace KeyboardSwitcher;

public sealed class CommandProcessor
{
    private readonly TextInputTracker _tracker;
    private readonly LanguageSwitcher _languageSwitcher;

    public CommandProcessor(
        TextInputTracker tracker,
        LanguageSwitcher languageSwitcher)
    {
        _tracker = tracker;
        _languageSwitcher = languageSwitcher;
    }

    public void Process(
        CommandEvent commandEvent)
    {
        DebugLog.Write(
            $"PROCESSOR: " +
            $"Command={commandEvent.Command} " +
            $"Action={commandEvent.Action} " +
            $"ActiveCount={_tracker.ActiveBuffer.Count} " +
            $"PendingCount={_tracker.PendingBuffer.Count}");

        if (commandEvent.Action != CommandAction.First)
            return;

        // --------------------------------------------------------
        // First:
        // поточний ActiveBuffer переносимо в PendingBuffer.
        // --------------------------------------------------------

        _tracker.SaveActiveToPending();

        DebugLog.Write(
            $"PROCESSOR: First handled. " +
            $"PendingCount={_tracker.PendingBuffer.Count} " +
            $"PendingHkl=0x{_tracker.PendingBuffer.SourceHkl.ToInt64():X} " +
            $"PendingHwnd=0x{_tracker.PendingBuffer.Hwnd.ToInt64():X}");

        // --------------------------------------------------------
        // Поки що тільки діагностика.
        //
        // Перемикання мови підключимо наступним кроком,
        // коли перевіримо правильність роботи PendingBuffer.
        // --------------------------------------------------------
    }
}