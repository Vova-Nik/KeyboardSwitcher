//namespace KeyboardSwitcher;

//public static class DebugLog
//{
//    private static readonly object _lock = new();

//    private static readonly string _logDirectory =
//        Path.Combine(
//            Environment.GetFolderPath(
//                Environment.SpecialFolder.LocalApplicationData),
//            "KeyboardLayoutConverterTest");

//    private static readonly string _logFile =
//        Path.Combine(
//            _logDirectory,
//            "debug.log");

//    public static void Write(string message)
//    {
//        lock (_lock)
//        {
//            Directory.CreateDirectory(_logDirectory);

//            File.AppendAllText(
//                _logFile,
//                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  " +
//                message +
//                Environment.NewLine);
//        }
//    }

//    public static void Clear()
//    {
//        lock (_lock)
//        {
//            Directory.CreateDirectory(_logDirectory);

//            File.WriteAllText(
//                _logFile,
//                string.Empty);
//        }
//    }
//}

namespace KeyboardSwitcher;

public static class DebugLog
{
    private static readonly object _lock = new();

    private static readonly string _logDirectory =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "KeyboardSwitcher");

    private static readonly string _logFile =
        Path.Combine(
            _logDirectory,
            "debug.log");

    static DebugLog()
    {
        Directory.CreateDirectory(_logDirectory);

        File.WriteAllText(
            _logFile,
            string.Empty);
    }

    public static void Write(string message)
    {
        lock (_lock)
        {
            Directory.CreateDirectory(_logDirectory);

            File.AppendAllText(
                _logFile,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  " +
                message +
                Environment.NewLine);
        }
    }

    public static void Clear()
    {
        lock (_lock)
        {
            Directory.CreateDirectory(_logDirectory);

            File.WriteAllText(
                _logFile,
                string.Empty);
        }
    }
}