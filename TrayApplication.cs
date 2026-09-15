using System.Drawing;
using System.Windows.Forms;

namespace KeyboardSwitcher;

public sealed class TrayApplication : IDisposable
{
    private readonly KeyboardLayouts _layouts;
    private readonly LanguageSwitcher _languageSwitcher;
    private readonly KeyboardHook _keyboardHook;
    private readonly NotifyIcon _trayIcon;
    private readonly ToolStripMenuItem _startItem;
    private readonly ToolStripMenuItem _stopItem;

    // Невидимий Control у UI-потоці.
    // Використовується для BeginInvoke().
    private readonly Control _uiInvoker;

    // Таймер для Help після тривалого утримання CapsLock.
    private readonly System.Windows.Forms.Timer _capsHelpTimer;

    private bool _disposed;

    public TrayApplication()
    {
        _layouts = new KeyboardLayouts();

        if (!_layouts.HasRequiredLanguages())
        {
            string missing = _layouts.GetMissingLanguages();

            MessageBox.Show(
                "Не знайдено необхідні розкладки Windows:\r\n\r\n" +
                missing,
                "KeyboardSwitcher",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            throw new ApplicationException(
                "Не знайдено необхідні розкладки.");
        }

        _languageSwitcher = new LanguageSwitcher(_layouts);
        _keyboardHook = new KeyboardHook();

        // --------------------------------------------------------
        // UI invoker
        // --------------------------------------------------------

        _uiInvoker = new Control();
        _uiInvoker.CreateControl();

        // --------------------------------------------------------
        // Help timer
        // --------------------------------------------------------

        _capsHelpTimer = new System.Windows.Forms.Timer
        {
            Interval = 2000
        };

        _capsHelpTimer.Tick += OnCapsHelpTimerTick;

        // --------------------------------------------------------
        // KeyboardHook events
        // --------------------------------------------------------

        _keyboardHook.LanguageRequested += OnLanguageRequested;

        _keyboardHook.CapsLockPressed += OnCapsLockPressed;
        _keyboardHook.CapsLockReleased += OnCapsLockReleased;
        _keyboardHook.CapsLockActivity += OnCapsLockActivity;

        // --------------------------------------------------------
        // Tray icon
        // --------------------------------------------------------

        _trayIcon = new NotifyIcon
        {
            Text = "KeyboardSwitcher",
            Icon = SystemIcons.Application,
            Visible = true
        };

        var menu = new ContextMenuStrip();

        _startItem = new ToolStripMenuItem("Start");
        _stopItem = new ToolStripMenuItem("Stop");
        var helpItem = new ToolStripMenuItem("Help");
        var exitItem = new ToolStripMenuItem("Exit");

        menu.Items.Add(_startItem);
        menu.Items.Add(_stopItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(helpItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        _trayIcon.ContextMenuStrip = menu;

        _startItem.Click += (_, _) => Start();
        _stopItem.Click += (_, _) => Stop();
        helpItem.Click += (_, _) => ShowHelp();
        exitItem.Click += (_, _) => Exit();

        _trayIcon.DoubleClick += (_, _) => ShowHelp();

        UpdateMenu();

        Start();
    }

    // ============================================================
    // Start / Stop
    // ============================================================

    private void Start()
    {
        if (_keyboardHook.IsRunning)
            return;

        if (!_keyboardHook.Start())
        {
            MessageBox.Show(
                "Не вдалося встановити keyboard hook.",
                "KeyboardSwitcher",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            return;
        }

        UpdateMenu();
    }

    private void Stop()
    {
        _capsHelpTimer.Stop();

        _keyboardHook.Stop();

        UpdateMenu();
    }

    // ============================================================
    // Language switching
    // ============================================================

    private LanguageAction OnLanguageRequested(
        LanguageKey language)
    {
        if (_disposed)
            return LanguageAction.SwitchLanguage;

        KeyboardLayout? current =
            _languageSwitcher.CurrentLayout;

        KeyboardLayout? target = language switch
        {
            LanguageKey.English => _layouts.English,
            LanguageKey.Russian => _layouts.Russian,
            LanguageKey.Ukrainian => _layouts.Ukrainian,
            _ => null
        };

        // Якщо цільової розкладки немає —
        // команду все одно поглинаємо.
        if (target == null)
            return LanguageAction.SwitchLanguage;

        // Якщо потрібна мова вже активна —
        // Caps+A/Z/Q працює як Shift+A/Z/Q.
        if (current != null &&
            current.Hkl == target.Hkl)
        {
            return LanguageAction.ShiftKey;
        }

        // Перемикання виконуємо АСИНХРОННО,
        // після завершення keyboard hook callback.
        //
        // Це необхідно, щоб A/Z/Q не потрапляли
        // в активну програму вже після зміни розкладки.
        try
        {
            _uiInvoker.BeginInvoke(
                new Action(() =>
                    SwitchLanguage(language)));
        }
        catch (InvalidOperationException)
        {
            // UI вже завершує роботу.
        }

        // Саму командну клавішу A/Z/Q поглинаємо.
        return LanguageAction.SwitchLanguage;
    }

    private void SwitchLanguage(
        LanguageKey language)
    {
        if (_disposed)
            return;

        switch (language)
        {
            case LanguageKey.English:
                _languageSwitcher.SwitchToEnglish();
                break;

            case LanguageKey.Russian:
                _languageSwitcher.SwitchToRussian();
                break;

            case LanguageKey.Ukrainian:
                _languageSwitcher.SwitchToUkrainian();
                break;
        }
    }

    // ============================================================
    // CapsLock Help
    // ============================================================

    private void OnCapsLockPressed()
    {
        if (_disposed)
            return;

        // Подія приходить з keyboard hook.
        // Таймер повинен запускатися в UI-потоці.
        try
        {
            _uiInvoker.BeginInvoke(
                new Action(() =>
                {
                    if (_disposed)
                        return;

                    _capsHelpTimer.Stop();
                    _capsHelpTimer.Start();
                }));
        }
        catch (InvalidOperationException)
        {
            // UI вже завершує роботу.
        }
    }

    private void OnCapsLockReleased()
    {
        if (_disposed)
            return;

        try
        {
            _uiInvoker.BeginInvoke(
                new Action(() =>
                {
                    if (_disposed)
                        return;

                    _capsHelpTimer.Stop();
                }));
        }
        catch (InvalidOperationException)
        {
            // UI вже завершує роботу.
        }
    }

    private void OnCapsLockActivity()
    {
        if (_disposed)
            return;

        try
        {
            _uiInvoker.BeginInvoke(
                new Action(() =>
                {
                    if (_disposed)
                        return;

                    _capsHelpTimer.Stop();
                }));
        }
        catch (InvalidOperationException)
        {
            // UI вже завершує роботу.
        }
    }

    private void OnCapsHelpTimerTick(
        object? sender,
        EventArgs e)
    {
        _capsHelpTimer.Stop();

        if (_disposed)
            return;

        if (!_keyboardHook.IsRunning)
            return;

        // Якщо ми дійшли сюди — CapsLock утримувався
        // приблизно 2 секунди без іншої клавіші.
        ShowHelp();
    }

    // ============================================================
    // Tray menu
    // ============================================================

    private void UpdateMenu()
    {
        _startItem.Enabled =
            !_keyboardHook.IsRunning;

        _stopItem.Enabled =
            _keyboardHook.IsRunning;
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
            "CapsLock + інша клавіша → Shift + клавіша\r\n\r\n" +
            "CapsLock не працює як звичайний CapsLock.\r\n" +
            "Стандартні засоби Windows для перемикання " +
            "розкладки не блокуються.",
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

        _capsHelpTimer.Stop();

        _keyboardHook.LanguageRequested -=
            OnLanguageRequested;

        _keyboardHook.CapsLockPressed -=
            OnCapsLockPressed;

        _keyboardHook.CapsLockReleased -=
            OnCapsLockReleased;

        _keyboardHook.CapsLockActivity -=
            OnCapsLockActivity;

        _keyboardHook.Dispose();

        _capsHelpTimer.Dispose();

        _uiInvoker.Dispose();

        _trayIcon.Visible = false;
        _trayIcon.Dispose();

        GC.SuppressFinalize(this);
    }
}