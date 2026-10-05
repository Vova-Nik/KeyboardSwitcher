using System.Drawing;
using System.Windows.Forms;

namespace KeyboardSwitcher;

public sealed class TrayApplication : IDisposable
{
    private readonly KeyboardLayouts _layouts;
    private readonly LanguageSwitcher _languageSwitcher;

    private KeyboardHook? _keyboardHook;

    private readonly TextInputTracker _tracker;
    private readonly TextInputCollector _collector;
    private readonly KeyboardLayoutContext _layoutContext;
    private readonly CommandProcessor _commandProcessor;
    private readonly CommandDetector _commandDetector;

    private readonly NotifyIcon _trayIcon;
    private readonly ToolStripMenuItem _startItem;
    private readonly ToolStripMenuItem _stopItem;
    private ShortcutHintForm? _shortcutHint;

    private bool _disposed;

    public TrayApplication()
    {
        _layouts = new KeyboardLayouts();

        if (!_layouts.HasRequiredLanguages())
        {
            string missing =
                _layouts.GetMissingLanguages();

            MessageBox.Show(
                "Не знайдено необхідні розкладки Windows:\r\n\r\n" +
                missing,
                "KeyboardSwitcher",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            throw new ApplicationException(
                "Не знайдено необхідні розкладки.");
        }

        _languageSwitcher =
            new LanguageSwitcher(_layouts);

        _tracker =
            new TextInputTracker();

        _layoutContext =
            new KeyboardLayoutContext();

        _collector =
            new TextInputCollector(
                _tracker,
                _layoutContext);

        _commandDetector =
            new CommandDetector();

        _commandDetector.CommandDetected +=
            OnCommandDetected;

        _commandDetector.KeyPassed +=
            _collector.Process;

        var keyboardLayoutConverter =
            new KeyboardLayoutConverter();

        var textLayoutConverter =
            new TextLayoutConverter(
                keyboardLayoutConverter);

        _commandProcessor =
            new CommandProcessor(
                _tracker,
                _languageSwitcher,
                keyboardLayoutConverter,
                textLayoutConverter);

        _commandDetector.CommandDetected +=
            _commandProcessor.Process;

        // --------------------------------------------------------
        // Tray icon
        // --------------------------------------------------------

        _trayIcon = new NotifyIcon
        {
            Text = "KeyboardSwitcher",
            Icon =
                Icon.ExtractAssociatedIcon(
                    Application.ExecutablePath)
                ?? SystemIcons.Application,
            Visible = true
        };

        var menu =
            new ContextMenuStrip();

        _startItem =
            new ToolStripMenuItem("Start");

        _stopItem =
            new ToolStripMenuItem("Stop");

        var helpItem =
            new ToolStripMenuItem("Help");

        var exitItem =
            new ToolStripMenuItem("Exit");

        menu.Items.Add(_startItem);
        menu.Items.Add(_stopItem);

        menu.Items.Add(
            new ToolStripSeparator());

        menu.Items.Add(helpItem);

        menu.Items.Add(
            new ToolStripSeparator());

        menu.Items.Add(exitItem);

        _trayIcon.ContextMenuStrip =
            menu;

        _startItem.Click +=
            (_, _) => Start();

        _stopItem.Click +=
            (_, _) => Stop();

        helpItem.Click +=
            (_, _) => ShowHelp();

        exitItem.Click +=
            (_, _) => Exit();

        _trayIcon.DoubleClick +=
            (_, _) => ShowHelp();

        UpdateMenu();

        Start();
    }


    // ============================================================
    // Start / Stop
    // ============================================================

    private void Start()
    {
        if (_disposed)
            return;

        if (_keyboardHook != null)
            return;

        try
        {
            _keyboardHook =
                new KeyboardHook();

            _keyboardHook.KeyPressed +=
                _commandDetector.Process;

            _keyboardHook.ShortcutHintRequested +=
                ShowShortcutHint;

            _keyboardHook.CapsLockReleased +=
                HideShortcutHint;

            DebugLog.Write(
                "KeyboardHook started.");

            //MessageBox.Show(
            //    "TrayApplication: KeyboardHook started",
            //     "TEST");
        }
        catch (Exception ex)
        {
            DebugLog.Write(
                $"KeyboardHook start failed: {ex}");

            _keyboardHook?.Dispose();
            _keyboardHook = null;

            MessageBox.Show(
                "Не вдалося встановити keyboard hook.\r\n\r\n" +
                ex.Message,
                "KeyboardSwitcher",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            return;
        }

        UpdateMenu();
    }

    private void ShowShortcutHint()
    {
        if (_shortcutHint == null ||
            _shortcutHint.IsDisposed)
        {
            _shortcutHint =
                new ShortcutHintForm();
        }

        _shortcutHint.ShowNearBottomRight();
    }


    private void HideShortcutHint()
    {
        if (_shortcutHint == null ||
            _shortcutHint.IsDisposed)
        {
            return;
        }

        _shortcutHint.Hide();
    }
    //private void Stop()
    //{
    //    if (_keyboardHook == null)
    //        return;

    //    _keyboardHook.KeyPressed -=
    //        _commandDetector.Process;

    //    _keyboardHook.Dispose();

    //    _keyboardHook = null;

    //    DebugLog.Write(
    //        "KeyboardHook stopped.");

    //    UpdateMenu();
    //}
    private void Stop()
    {
        HideShortcutHint();

        if (_keyboardHook == null)
            return;

        _keyboardHook.KeyPressed -=
            _commandDetector.Process;

        _keyboardHook.Dispose();

        _keyboardHook = null;

        DebugLog.Write(
            "KeyboardHook stopped.");

        UpdateMenu();
    }

    // ============================================================
    // CommandDetector
    // ============================================================

    private void OnCommandDetected(
        CommandEvent commandEvent)
    {
        DebugLog.Write(
            $"COMMAND: " +
            $"Command={commandEvent.Command} " +
            $"Action={commandEvent.Action}");
    }


    // ============================================================
    // Tray menu
    // ============================================================

    private void UpdateMenu()
    {
        bool running =
            _keyboardHook != null;

        _startItem.Enabled =
            !running;

        _stopItem.Enabled =
            running;
    }


    private void ShowHelp()
    {
        if (_disposed)
            return;

        MessageBox.Show(
            "KeyboardSwitcher\r\n\r\n" +
            "CapsLock + A  →  English\r\n" +
            "CapsLock + Z  →  Russian\r\n" +
            "CapsLock + Q  →  Ukrainian\r\n\r\n" +
            "Поточна версія використовує новий\r\n" +
            "CommandDetector / TextInputCollector.",
            "KeyboardSwitcher — Help",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }


    // ============================================================
    // Exit / Dispose
    // ============================================================

    private void Exit()
    {
        Dispose();

        Application.Exit();
    }


    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _commandDetector.CommandDetected -=
            OnCommandDetected;

        _commandDetector.KeyPassed -=
            _collector.Process;

        _commandDetector.CommandDetected -=
            _commandProcessor.Process;

        _shortcutHint?.Dispose();

        _keyboardHook.ShortcutHintRequested -=
            ShowShortcutHint;

        _keyboardHook.CapsLockReleased -=
            HideShortcutHint;

        Stop();

        _trayIcon.Visible = false;
        _trayIcon.Dispose();

        GC.SuppressFinalize(this);
    }
}