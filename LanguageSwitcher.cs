using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace KeyboardSwitcher;

public sealed class LanguageSwitcher
{
    private const uint WM_INPUTLANGCHANGEREQUEST = 0x0050;

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    private const ushort VK_LSHIFT = 0xA0;
    private const ushort VK_LCONTROL = 0xA2;
    private const ushort VK_LMENU = 0xA4;
    private const ushort VK_LWIN = 0x5B;
    private const ushort VK_SPACE = 0x20;
    private const ushort VK_OEM_3 = 0xC0;     // ` / ~


    private enum LanguageSwitchHotkey
    {
        AltShift,
        CtrlShift,
        GraveAccent,
        WinSpace
    }


    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }


    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }


    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }


    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();


    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(
        IntPtr hWnd,
        IntPtr lpdwProcessId);


    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(
        uint idThread);


    [DllImport("user32.dll")]
    private static extern IntPtr PostMessage(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam);


    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(
        uint cInputs,
        INPUT[] pInputs,
        int cbSize);


    private readonly KeyboardLayouts _layouts;

    private readonly LanguageSwitchHotkey _systemSwitchHotkey;


    public LanguageSwitcher(
        KeyboardLayouts layouts)
    {
        _layouts = layouts;

        _systemSwitchHotkey =
            DetectSystemSwitchHotkey();

        //MessageBox.Show(
        //    $"LanguageSwitcher запущено\n" +
        //    $"System switch: {SystemSwitchShortcut}",
        //    "DIAGNOSTIC");
    }


    // ------------------------------------------------------------
    // Поточна розкладка активного вікна.
    // ------------------------------------------------------------

    /*  public KeyboardLayout? CurrentLayout
      {
          get
          {
              IntPtr currentHkl =
                  GetCurrentHkl();

              if (currentHkl == IntPtr.Zero)
                  return null;


              foreach (KeyboardLayout layout in _layouts.All)
              {
                  if (layout.Hkl == currentHkl)
                      return layout;
              }


              return null;
          }
      }*/
    public KeyboardLayout? CurrentLayout
    {
        get
        {
            IntPtr window = GetForegroundWindow();

            if (window == IntPtr.Zero)
                return null;

            uint threadId =
                GetWindowThreadProcessId(
                    window,
                    IntPtr.Zero);

            if (threadId == 0)
                return null;

            IntPtr hkl =
                GetKeyboardLayout(threadId);

            KeyboardLayout? layout = null;

            foreach (KeyboardLayout item in _layouts.All)
            {
                if (item.Hkl == hkl)
                {
                    layout = item;
                    break;
                }
            }

            MessageBox.Show(
                $"HWND: 0x{window.ToInt64():X}\n" +
                $"Thread ID: {threadId}\n" +
                $"HKL: 0x{hkl.ToInt64():X}\n" +
                $"Layout: {layout?.DisplayName ?? "UNKNOWN"}\n" +
                $"Locale: {layout?.LocaleName ?? "UNKNOWN"}\n" +
                $"System switch: {SystemSwitchShortcut}",
                "KeyboardSwitcher diagnostic");

            return layout;
        }
    }


    // ------------------------------------------------------------
    // Для діагностики.
    // Наприклад, можна буде показати це в Help.
    // ------------------------------------------------------------

    public string SystemSwitchShortcut
    {
        get
        {
            return _systemSwitchHotkey switch
            {
                LanguageSwitchHotkey.AltShift =>
                    "Left Alt + Shift",

                LanguageSwitchHotkey.CtrlShift =>
                    "Ctrl + Shift",

                LanguageSwitchHotkey.GraveAccent =>
                    "`",

                LanguageSwitchHotkey.WinSpace =>
                    "Win + Space",

                _ =>
                    "Unknown"
            };
        }
    }


    public bool SwitchToEnglish()
    {
        return SwitchTo(
            _layouts.English);
    }


    public bool SwitchToRussian()
    {
        return SwitchTo(
            _layouts.Russian);
    }


    public bool SwitchToUkrainian()
    {
        return SwitchTo(
            _layouts.Ukrainian);
    }


    // ============================================================
    // Основне перемикання
    // ============================================================

    private bool SwitchTo(
        KeyboardLayout? targetLayout)
    {
        if (targetLayout == null)
            return false;


        // --------------------------------------------------------
        // Якщо потрібна розкладка вже активна —
        // нічого робити не треба.
        // --------------------------------------------------------

        if (IsCurrentLayout(targetLayout))
            return true;


        // --------------------------------------------------------
        // Спочатку використовуємо наш старий, прямий спосіб.
        // Він швидкий і добре працює у звичайних вікнах.
        // --------------------------------------------------------

        TryDirectSwitch(
            targetLayout);


        // PostMessage асинхронний.
        // Даємо Windows трохи часу застосувати зміну.
        Thread.Sleep(40);


        if (IsCurrentLayout(targetLayout))
            return true;


        // --------------------------------------------------------
        // Пряме перемикання не спрацювало.
        //
        // Тепер циклічно використовуємо системну комбінацію
        // перемикання мов і після кожної спроби перевіряємо
        // фактичний HKL.
        //
        // Нас не цікавить порядок мов і наявність додаткових
        // Spanish, German тощо.
        // --------------------------------------------------------

        int maxAttempts =
            Math.Max(
                _layouts.All.Count,
                1);


        for (int attempt = 0;
             attempt < maxAttempts;
             attempt++)
        {
            if (!SendSystemSwitchHotkey())
                return false;


            Thread.Sleep(40);


            if (IsCurrentLayout(targetLayout))
                return true;
        }


        return false;
    }


    // ============================================================
    // Поточний HKL
    // ============================================================

    private IntPtr GetCurrentHkl()
    {
        IntPtr window =
            GetForegroundWindow();


        if (window == IntPtr.Zero)
            return IntPtr.Zero;


        uint threadId =
            GetWindowThreadProcessId(
                window,
                IntPtr.Zero);


        if (threadId == 0)
            return IntPtr.Zero;


        return GetKeyboardLayout(
            threadId);
    }


    private bool IsCurrentLayout(
        KeyboardLayout layout)
    {
        IntPtr currentHkl =
            GetCurrentHkl();


        return currentHkl != IntPtr.Zero &&
               currentHkl == layout.Hkl;
    }


    // ============================================================
    // Пряме перемикання через WM_INPUTLANGCHANGEREQUEST
    // ============================================================

    private bool TryDirectSwitch(
        KeyboardLayout layout)
    {
        IntPtr window =
            GetForegroundWindow();


        if (window == IntPtr.Zero)
            return false;


        IntPtr result =
            PostMessage(
                window,
                WM_INPUTLANGCHANGEREQUEST,
                IntPtr.Zero,
                layout.Hkl);


        return result != IntPtr.Zero;
    }


    // ============================================================
    // Визначення системної комбінації перемикання мов
    // ============================================================

    private static LanguageSwitchHotkey DetectSystemSwitchHotkey()
    {
        try
        {
            using RegistryKey? key =
                Registry.CurrentUser.OpenSubKey(
                    @"Keyboard Layout\Toggle");


            if (key != null)
            {
                // ------------------------------------------------
                // Саме Language Hotkey відповідає за
                // "Between input languages".
                //
                // На різних версіях Windows зустрічається також
                // Hotkey, тому залишаємо його як резерв.
                // ------------------------------------------------

                string? value =
                    key.GetValue(
                        "Language Hotkey")?.ToString();


                if (string.IsNullOrWhiteSpace(value))
                {
                    value =
                        key.GetValue(
                            "Hotkey")?.ToString();
                }


                switch (value)
                {
                    case "1":
                        return LanguageSwitchHotkey.AltShift;

                    case "2":
                        return LanguageSwitchHotkey.CtrlShift;

                    case "4":
                        return LanguageSwitchHotkey.GraveAccent;
                }
            }
        }
        catch
        {
            // ----------------------------------------------------
            // Реєстр тут не критичний.
            // Якщо прочитати настройку не вдалося,
            // використовуємо Win+Space.
            // ----------------------------------------------------
        }


        // --------------------------------------------------------
        // Значення 3 означає, що Alt+Shift / Ctrl+Shift вимкнені.
        // Win+Space використовуємо як універсальний резерв.
        // --------------------------------------------------------

        return LanguageSwitchHotkey.WinSpace;
    }


    // ============================================================
    // Посилка системної комбінації
    // ============================================================

    private bool SendSystemSwitchHotkey()
    {
        return _systemSwitchHotkey switch
        {
            LanguageSwitchHotkey.AltShift =>
                SendTwoKeyCombination(
                    VK_LMENU,
                    VK_LSHIFT),

            LanguageSwitchHotkey.CtrlShift =>
                SendTwoKeyCombination(
                    VK_LCONTROL,
                    VK_LSHIFT),

            LanguageSwitchHotkey.GraveAccent =>
                SendSingleKey(
                    VK_OEM_3),

            LanguageSwitchHotkey.WinSpace =>
                SendTwoKeyCombination(
                    VK_LWIN,
                    VK_SPACE),

            _ =>
                false
        };
    }


    // ============================================================
    // SendInput helpers
    // ============================================================

    private static bool SendSingleKey(
        ushort key)
    {
        INPUT[] inputs =
        {
            CreateKeyInput(
                key,
                false),

            CreateKeyInput(
                key,
                true)
        };


        uint sent =
            SendInput(
                (uint)inputs.Length,
                inputs,
                Marshal.SizeOf<INPUT>());


        return sent ==
               inputs.Length;
    }


    private static bool SendTwoKeyCombination(
        ushort modifier,
        ushort key)
    {
        INPUT[] inputs =
        {
            CreateKeyInput(
                modifier,
                false),

            CreateKeyInput(
                key,
                false),

            CreateKeyInput(
                key,
                true),

            CreateKeyInput(
                modifier,
                true)
        };


        uint sent =
            SendInput(
                (uint)inputs.Length,
                inputs,
                Marshal.SizeOf<INPUT>());


        return sent ==
               inputs.Length;
    }


    private static INPUT CreateKeyInput(
        ushort virtualKey,
        bool keyUp)
    {
        return new INPUT
        {
            type = INPUT_KEYBOARD,

            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = virtualKey,
                    wScan = 0,

                    dwFlags =
                        keyUp
                            ? KEYEVENTF_KEYUP
                            : 0,

                    time = 0,
                    dwExtraInfo = UIntPtr.Zero
                }
            }
        };
    }
}