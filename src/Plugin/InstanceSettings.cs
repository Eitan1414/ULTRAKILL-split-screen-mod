namespace ULTRAKILLSplitScreen.Plugin;

internal sealed class InstanceSettings
{
    public int PlayerIndex { get; private set; } = 1;
    public int PlayerCount { get; private set; } = 2;
    public int Width { get; private set; } = 960;
    public int Height { get; private set; } = 540;
    public bool Muted { get; private set; }
    public bool InputIsolation { get; private set; } = true;
    public int GamepadIndex { get; private set; }
    public bool JaketEnabled { get; private set; }
    public bool JaketHost { get; private set; }
    public string JaketCodeFile { get; private set; } = string.Empty;
    public int JaketStartDelaySeconds { get; private set; } = 8;
    public int JaketTimeoutSeconds { get; private set; } = 60;

    public static InstanceSettings FromEnvironment()
    {
        return new InstanceSettings
        {
            PlayerIndex = ReadInt("UKSS_PLAYER_INDEX", 1, 1, 4),
            PlayerCount = ReadInt("UKSS_PLAYER_COUNT", 2, 1, 4),
            Width = ReadInt("UKSS_WINDOW_WIDTH", 960, 320, 16384),
            Height = ReadInt("UKSS_WINDOW_HEIGHT", 540, 240, 16384),
            Muted = ReadBool("UKSS_MUTED"),
            InputIsolation = ReadBool("UKSS_INPUT_ISOLATION", true),
            GamepadIndex = ReadInt("UKSS_GAMEPAD_INDEX", 0, -1, 31),
            JaketEnabled = ReadBool("UKSS_JAKET_ENABLED"),
            JaketHost = ReadBool("UKSS_JAKET_HOST"),
            JaketCodeFile = Environment.GetEnvironmentVariable("UKSS_JAKET_CODE_FILE") ?? string.Empty,
            JaketStartDelaySeconds = ReadInt("UKSS_JAKET_START_DELAY", 8, 1, 120),
            JaketTimeoutSeconds = ReadInt("UKSS_JAKET_TIMEOUT", 60, 10, 300)
        };
    }

    private static int ReadInt(string name, int fallback, int minimum, int maximum)
    {
        string? raw = Environment.GetEnvironmentVariable(name);
        return int.TryParse(raw, out int value)
            ? Math.Clamp(value, minimum, maximum)
            : fallback;
    }

    private static bool ReadBool(string name, bool fallback = false)
    {
        string? raw = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(raw))
            return fallback;
        return raw == "1" || bool.TryParse(raw, out bool parsed) && parsed;
    }
}
