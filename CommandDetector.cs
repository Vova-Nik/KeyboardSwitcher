using System.Diagnostics;
using System.Windows.Forms;

namespace KeyboardSwitcher;

public enum LayoutCommand
{
    None,
    Ukrainian,
    English,
    Russian
}

public enum CommandAction
{
    None,
    Single,
    Double
}

public sealed class CommandDetector
{
    //private const long DoublePressLimitMs = 300;

    private readonly object _lock = new();

    // Попередня повна команда Caps+X.
    private LayoutCommand _pendingCommand =
        LayoutCommand.None;

    // Час завершення першої команди — тобто момент A/Z/Q.
    private long _pendingCommandTime;

    private System.Threading.Timer? _timer;

    public event Action<CommandEvent>? CommandDetected;

    public event Action<KeyInfo>? KeyPassed;

    public bool Process(KeyInfo info)
    {
        if (info.IsConverterInput)
            return false;

        // Сам CapsLock — тільки модифікатор.
        // До CommandDetector окремо він не передається.
        if (info.Key == Keys.Capital)
            return true;

        LayoutCommand command =
            GetCommand(info);

        if (command == LayoutCommand.None)
        {
            KeyPassed?.Invoke(info);
            return false;
        }

        lock (_lock)
        {
            long now =
                Stopwatch.GetTimestamp();

            // Немає попередньої Caps+X.
            if (_pendingCommand == LayoutCommand.None)
            {
                StartPendingCommand(
                    command,
                    now);

                DebugLog.Write(
                    $"COMMAND: First command. " +
                    $"Command={command}. " +
                    $"Starting 300ms double window.");

                return true;
            }

            long elapsedMs =
                (now - _pendingCommandTime) *
                1000 /
                Stopwatch.Frequency;

            // Та сама команда в межах 300 мс:
            // Caps+A + Caps+A = Double.
            if (command == _pendingCommand &&
               elapsedMs <= Config.DoubleClickMaxTimeMs)
            {
                CancelTimer();

                LayoutCommand doubleCommand =
                    _pendingCommand;

                _pendingCommand =
                    LayoutCommand.None;

                _pendingCommandTime = 0;

                DebugLog.Write(
                    $"COMMAND: Command={doubleCommand} " +
                    $"Action=Double " +
                    $"Elapsed={elapsedMs}ms");

                CommandDetected?.Invoke(
                    new CommandEvent(
                        doubleCommand,
                        CommandAction.Double));

                return true;
            }

            // Була інша команда або минуло більше 300 мс.
            //
            // Попередня команда тепер однозначно Single.
            LayoutCommand previousCommand =
                _pendingCommand;

            CancelTimer();

            _pendingCommand =
                LayoutCommand.None;

            _pendingCommandTime = 0;

            DebugLog.Write(
                $"COMMAND: Command={previousCommand} " +
                $"Action=Single " +
                $"Elapsed={elapsedMs}ms");

            CommandDetected?.Invoke(
                new CommandEvent(
                    previousCommand,
                    CommandAction.Single));

            // Поточна команда стає першою
            // для наступного можливого Double.
            StartPendingCommand(
                command,
                now);

            DebugLog.Write(
                $"COMMAND: First command. " +
                $"Command={command}. " +
                $"Starting 300ms double window.");

            return true;
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            CancelTimer();

            _pendingCommand =
                LayoutCommand.None;

            _pendingCommandTime = 0;
        }
    }

    private void StartPendingCommand(
        LayoutCommand command,
        long timestamp)
    {
        _pendingCommand =
            command;

        _pendingCommandTime =
            timestamp;

        _timer = new System.Threading.Timer(
            _ =>
            {
                CompleteSingleCommand();
            },
            null,
            Config.DecisionPauseTimeMs,
            Timeout.Infinite);
    }

    private void CompleteSingleCommand()
    {
        lock (_lock)
        {
            if (_pendingCommand == LayoutCommand.None)
                return;

            long now =
                Stopwatch.GetTimestamp();

            long elapsedMs =
                (now - _pendingCommandTime) *
                1000 /
                Stopwatch.Frequency;

            // Захист від можливого раннього
            // спрацювання Timer.
            //if (elapsedMs < DoublePressLimitMs)
            //    return;
            if (elapsedMs < Config.DecisionPauseTimeMs)
                return;

            LayoutCommand command =
                _pendingCommand;

            _pendingCommand =
                LayoutCommand.None;

            _pendingCommandTime = 0;

            _timer?.Dispose();
            _timer = null;

            DebugLog.Write(
                $"COMMAND: Command={command} " +
                $"Action=Single " +
                $"Elapsed={elapsedMs}ms");

            CommandDetected?.Invoke(
                new CommandEvent(
                    command,
                    CommandAction.Single));
        }
    }

    private void CancelTimer()
    {
        _timer?.Dispose();
        _timer = null;
    }

    private static LayoutCommand GetCommand(
        KeyInfo info)
    {
        if (!info.CapsLockHeld)
            return LayoutCommand.None;

        return info.Key switch
        {
            Keys.Q => LayoutCommand.Ukrainian,
            Keys.A => LayoutCommand.English,
            Keys.Z => LayoutCommand.Russian,
            _ => LayoutCommand.None
        };
    }
}