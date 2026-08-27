using System.Globalization;
using System.Runtime.InteropServices;

namespace KeyboardSwitcher;

public sealed class KeyboardLayout
{
    public IntPtr Hkl { get; }

    public string LocaleName { get; }

    public string DisplayName { get; }

    public KeyboardLayout(
        IntPtr hkl,
        string localeName,
        string displayName)
    {
        Hkl = hkl;
        LocaleName = localeName;
        DisplayName = displayName;
    }

    public override string ToString()
    {
        return
            $"{DisplayName} [{LocaleName}] " +
            $"HKL = 0x{Hkl.ToInt64():X}";
    }
}


public sealed class KeyboardLayouts
{
    [DllImport("user32.dll")]
    private static extern int GetKeyboardLayoutList(
        int nBuff,
        [Out] IntPtr[] lpList);


    private readonly List<KeyboardLayout> _layouts = new();


    public IReadOnlyList<KeyboardLayout> All =>
        _layouts;


    public KeyboardLayout? English { get; private set; }

    public KeyboardLayout? Russian { get; private set; }

    public KeyboardLayout? Ukrainian { get; private set; }


    public KeyboardLayouts()
    {
        Load();
    }


    private void Load()
    {
        _layouts.Clear();

        English = null;
        Russian = null;
        Ukrainian = null;


        // --------------------------------------------------------
        // Спочатку дізнаємося кількість HKL.
        // --------------------------------------------------------

        int count =
            GetKeyboardLayoutList(
                0,
                Array.Empty<IntPtr>());


        if (count <= 0)
            return;


        var hkls = new IntPtr[count];


        int actualCount =
            GetKeyboardLayoutList(
                count,
                hkls);


        for (int i = 0; i < actualCount; i++)
        {
            IntPtr hkl = hkls[i];


            // Нижні 16 біт HKL = LANGID.
            ushort langId =
                unchecked(
                    (ushort)hkl.ToInt64());


            try
            {
                CultureInfo culture =
                    CultureInfo.GetCultureInfo(
                        langId);


                var layout = new KeyboardLayout(
                    hkl,
                    culture.Name,
                    culture.DisplayName);


                _layouts.Add(layout);


                // ------------------------------------------------
                // Автоматично вибираємо одну розкладку кожної
                // потрібної мови.
                // ------------------------------------------------

                if (culture.Name.StartsWith(
                        "en-",
                        StringComparison.OrdinalIgnoreCase)
                    && English == null)
                {
                    English = layout;
                }


                if (culture.Name.StartsWith(
                        "ru-",
                        StringComparison.OrdinalIgnoreCase)
                    && Russian == null)
                {
                    Russian = layout;
                }


                if (culture.Name.StartsWith(
                        "uk-",
                        StringComparison.OrdinalIgnoreCase)
                    && Ukrainian == null)
                {
                    Ukrainian = layout;
                }
            }
            catch
            {
                // LANGID невідомий .NET.
                // Для нашої задачі такий HKL пропускаємо.
            }
        }
    }


    public bool HasRequiredLanguages()
    {
        return
            English != null &&
            Russian != null &&
            Ukrainian != null;
    }


    public string GetMissingLanguages()
    {
        var missing = new List<string>();


        if (English == null)
            missing.Add("English");


        if (Russian == null)
            missing.Add("Russian");


        if (Ukrainian == null)
            missing.Add("Ukrainian");


        return string.Join(
            Environment.NewLine,
            missing);
    }
}