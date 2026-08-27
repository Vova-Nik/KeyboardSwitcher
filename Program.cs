namespace KeyboardSwitcher;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        using var application = new TrayApplication();

        Application.Run();
    }
}