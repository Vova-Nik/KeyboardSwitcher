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
    First,
    Second
}

public sealed class CommandDetector
{
    private const long SecondPressLimitMs = 300;
    private const long NewCommandLimitMs = 500;

    private LayoutCommand _lastCommand =
        LayoutCommand.None;

    private long _lastCommandTime;

    public event Action<CommandEvent>? CommandDetected;

    public event Action<KeyInfo>? KeyPassed;

    //public void Process(KeyInfo info)
    //{
    //    // Синтетичний ввід конвертера
    //    // не повинен проходити через командний детектор.
    //    if (info.IsConverterInput)
    //        return;

    //    LayoutCommand command =
    //        GetCommand(info);

    //    if (command == LayoutCommand.None)
    //    {
    //        KeyPassed?.Invoke(info);
    //        return;
    //    }

    //    long now =
    //        Stopwatch.GetTimestamp();

    //    CommandAction action =
    //        DetectAction(
    //            command,
    //            now);

    //    if (action == CommandAction.None)
    //        return;

    //    CommandDetected?.Invoke(
    //        new CommandEvent(
    //            command,
    //            action));
    //}
    public bool Process(KeyInfo info)
    {
        if (info.IsConverterInput)
            return false;

        LayoutCommand command =
            GetCommand(info);

        if (command == LayoutCommand.None)
        {
            KeyPassed?.Invoke(info);
            return false;
        }

        long now =
            Stopwatch.GetTimestamp();

        CommandAction action =
            DetectAction(command, now);

        if (action != CommandAction.None)
        {
            CommandDetected?.Invoke(
                new CommandEvent(command, action));
        }

        // Це командна клавіша Caps+A/Z/Q.
        // Не передаємо її далі в Windows.
        return true;
    }

    public void Reset()
    {
        _lastCommand =
            LayoutCommand.None;

        _lastCommandTime = 0;
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

    private CommandAction DetectAction(
        LayoutCommand command,
        long now)
    {
        if (_lastCommand == LayoutCommand.None)
        {
            StartNewCommand(
                command,
                now);

            return CommandAction.First;
        }

        long elapsedMs =
            (now - _lastCommandTime) * 1000 /
            Stopwatch.Frequency;

        if (command == _lastCommand &&
            elapsedMs <= SecondPressLimitMs)
        {
            _lastCommand =
                LayoutCommand.None;

            _lastCommandTime = 0;

            return CommandAction.Second;
        }

        if (command == _lastCommand &&
            elapsedMs <= NewCommandLimitMs)
        {
            return CommandAction.None;
        }

        StartNewCommand(
            command,
            now);

        return CommandAction.First;
    }

    private void StartNewCommand(
        LayoutCommand command,
        long time)
    {
        _lastCommand = command;
        _lastCommandTime = time;
    }
}