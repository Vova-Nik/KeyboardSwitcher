namespace KeyboardSwitcher;

public sealed class CommandEvent
{
    public LayoutCommand Command { get; }

    public CommandAction Action { get; }

    public CommandEvent(
        LayoutCommand command,
        CommandAction action)
    {
        Command = command;
        Action = action;
    }
}