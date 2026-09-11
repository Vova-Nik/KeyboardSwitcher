using System.Drawing;

namespace KeyboardSwitcher;

public sealed class TrayApplication : IDisposable
{
    private readonly KeyboardLayouts _layouts;

    private readonly LanguageSwitcher _languageSwitcher;

    private readonly KeyboardHook _keyboardHook;

    private readonly NotifyIcon _trayIcon;

    private readonly ToolStripMenuItem _startItem;

    private readonly ToolStripMenuItem _stopItem;

    private bool _disposed;


    public TrayApplication()
    {
        // ========================================================
        // 1. Знаходимо розкладки
        // ========================================================

        _layouts =
            new KeyboardLayouts();


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


        // ========================================================
        // 2. Створюємо компоненти
        // ========================================================

        _languageSwitcher =
            new LanguageSwitcher(
                _layouts);


        _keyboardHook =
            new KeyboardHook();


        // ========================================================
        // 3. Підписуємося на команди hook
        // ========================================================

        _keyboardHook.LanguageRequested +=
            OnLanguageRequested;


        // ========================================================
        // 4. Створюємо Tray icon
        // ========================================================

        _trayIcon =
            new NotifyIcon
            {
                Text = "KeyboardSwitcher",

                Icon =
                    SystemIcons.Application,

                Visible = true
            };


        // ========================================================
        // Context menu
        // ========================================================

        var menu =
            new ContextMenuStrip();


        _startItem =
            new ToolStripMenuItem(
                "Start");


        _stopItem =
            new ToolStripMenuItem(
                "Stop");


        var helpItem =
            new ToolStripMenuItem(
                "Help");


        var exitItem =
            new ToolStripMenuItem(
                "Exit");


        menu.Items.Add(
            _startItem);


        menu.Items.Add(
            _stopItem);


        menu.Items.Add(
            new ToolStripSeparator());


        menu.Items.Add(
            helpItem);


        menu.Items.Add(
            new ToolStripSeparator());


        menu.Items.Add(
            exitItem);


        _trayIcon.ContextMenuStrip =
            menu;


        // ========================================================
        // Menu events
        // ========================================================

        _startItem.Click +=
            (_, _) => Start();


        _stopItem.Click +=
            (_, _) => Stop();


        helpItem.Click +=
            (_, _) => ShowHelp();


        exitItem.Click +=
            (_, _) => Exit();


        // ========================================================
        // Подвійний клік по іконці
        // ========================================================

        _trayIcon.DoubleClick +=
            (_, _) => ShowHelp();


        // ========================================================
        // Початковий стан
        // ========================================================

        UpdateMenu();


        // ========================================================
        // Автоматично запускаємо hook.
        // ========================================================

        Start();
    }


    // ============================================================
    // Start
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


    // ============================================================
    // Stop
    // ============================================================

    private void Stop()
    {
        _keyboardHook.Stop();

        UpdateMenu();
    }


    // ============================================================
    // Language requested
    // ============================================================

    private LanguageAction OnLanguageRequested(
    LanguageKey language)
    {
        KeyboardLayout? current =
            _languageSwitcher.CurrentLayout;

        KeyboardLayout? target =
            language switch
            {
                LanguageKey.English =>
                    _layouts.English,

                LanguageKey.Russian =>
                    _layouts.Russian,

                LanguageKey.Ukrainian =>
                    _layouts.Ukrainian,

                _ =>
                    null
            };

        // Якщо цільова розкладка не знайдена —
        // це має бути перемикання.
        if (target == null)
            return LanguageAction.SwitchLanguage;

        // Якщо ми вже на потрібній мові,
        // Caps+A/Z/Q працює як Shift+клавіша.
        if (current != null &&
            current.Hkl == target.Hkl)
        {
            return LanguageAction.ShiftKey;
        }

        // Інакше це команда перемикання мови.
        bool success =
            language switch
            {
                LanguageKey.English =>
                    _languageSwitcher.SwitchToEnglish(),

                LanguageKey.Russian =>
                    _languageSwitcher.SwitchToRussian(),

                LanguageKey.Ukrainian =>
                    _languageSwitcher.SwitchToUkrainian(),

                _ =>
                    false
            };

        _ = success;

        return LanguageAction.SwitchLanguage;
    }


    // ============================================================
    // Tray menu state
    // ============================================================

    private void UpdateMenu()
    {
        _startItem.Enabled =
            !_keyboardHook.IsRunning;


        _stopItem.Enabled =
            _keyboardHook.IsRunning;
    }


    // ============================================================
    // Help
    // ============================================================

    private void ShowHelp()
    {
        MessageBox.Show(
            "KeyboardSwitcher\r\n\r\n" +

            "CapsLock + A  →  English\r\n" +
            "CapsLock + Z  →  Russian\r\n" +
            "CapsLock + Q  →  Ukrainian\r\n\r\n" +

            "CapsLock не працює як звичайний CapsLock.\r\n" +
            "Стандартні засоби Windows для перемикання " +
            "розкладки не блокуються.",

            "KeyboardSwitcher — Help",

            MessageBoxButtons.OK,

            MessageBoxIcon.Information);
    }


    // ============================================================
    // Exit
    // ============================================================

    private void Exit()
    {
        Dispose();

        Application.Exit();
    }


    // ============================================================
    // Dispose
    // ============================================================

    public void Dispose()
    {
        if (_disposed)
            return;


        _keyboardHook.LanguageRequested -=
            OnLanguageRequested;


        _keyboardHook.Dispose();


        _trayIcon.Visible = false;


        _trayIcon.Dispose();


        _disposed = true;


        GC.SuppressFinalize(this);
    }
}