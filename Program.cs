using System.Threading;

namespace KeyboardSwitcher;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        using Mutex mutex =
            new Mutex(
                true,
                @"Local\KeyboardSwitcher",
                out bool createdNew);

        if (!createdNew)
        {
            MessageBox.Show(
                "KeyboardSwitcher не запущено.\r\n\r\n" +
                "Інша копія програми вже працює.",
                "KeyboardSwitcher",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            return;
        }

        ApplicationConfiguration.Initialize();

        using var application =
            new TrayApplication();

        Application.Run();
    }
}