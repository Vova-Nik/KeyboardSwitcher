namespace KeyboardSwitcher;

public static class Config
{
    private const int DefaultDoubleClickMaxTimeMs = 300;
    private const int DefaultDecisionPauseTimeMs = 500;

    private const int MinTimeMs = 50;
    private const int MaxTimeMs = 1000;

    public static int DoubleClickMaxTimeMs { get; private set; }

    public static int DecisionPauseTimeMs { get; private set; }

    public static void Load()
    {
        DoubleClickMaxTimeMs =
            DefaultDoubleClickMaxTimeMs;

        DecisionPauseTimeMs =
            DefaultDecisionPauseTimeMs;

        string configFile =
            Path.Combine(
                AppContext.BaseDirectory,
                "config.txt");

        if (!File.Exists(configFile))
        {
            CreateDefaultConfig(configFile);
            return;
        }

        int? doubleClickMax = null;
        int? decisionPause = null;

        foreach (string rawLine in File.ReadAllLines(configFile))
        {
            string line = rawLine.Trim();

            if (string.IsNullOrEmpty(line))
                continue;

            if (line.StartsWith("#"))
                continue;

            string[] parts =
                line.Split(
                    ':',
                    2,
                    StringSplitOptions.TrimEntries);

            if (parts.Length != 2)
                continue;

            string name =
                parts[0].ToLowerInvariant();

            string value =
                parts[1];

            if (!int.TryParse(
                    value,
                    out int number))
                continue;

            switch (name)
            {
                case "doubleclick max time(ms)":
                    doubleClickMax = number;
                    break;

                case "decision pause time(ms)":
                    decisionPause = number;
                    break;
            }
        }

        if (!IsValid(doubleClickMax) ||
            !IsValid(decisionPause) ||
            decisionPause < doubleClickMax)
        {
            DoubleClickMaxTimeMs =
                DefaultDoubleClickMaxTimeMs;

            DecisionPauseTimeMs =
                DefaultDecisionPauseTimeMs;

            CreateDefaultConfig(configFile);
            return;
        }

        DoubleClickMaxTimeMs =
            doubleClickMax!.Value;

        DecisionPauseTimeMs =
            decisionPause!.Value;
    }

    private static bool IsValid(int? value)
    {
        return value.HasValue &&
               value.Value >= MinTimeMs &&
               value.Value <= MaxTimeMs;
    }

    private static void CreateDefaultConfig(
        string configFile)
    {
        string[] lines =
        {
            "# KeyboardSwitcher configuration",
            "#",
            "# Maximum time between two identical",
            "# Caps+X commands to recognize them as Double.",
            "# Allowed range: 50...1000 ms.",
            "doubleclick max time(ms): 300",
            "",
            "# Time to wait after the first Caps+X command",
            "# before deciding that it was Single.",
            "# Allowed range: 50...1000 ms.",
            "# This value must be greater than or equal",
            "# to doubleclick max time(ms).",
            "decision pause time(ms): 500"
        };

        File.WriteAllLines(
            configFile,
            lines);
    }
}