namespace ULTRAKILLSplitScreen.Plugin;

internal sealed class InstanceSettings
{
    public int PlayerIndex { get; private init; } = 1;
    public int Width { get; private init; } = 960;
    public int Height { get; private init; } = 1080;
    public bool Muted { get; private init; }

    public static InstanceSettings FromEnvironment()
    {
        return new InstanceSettings
        {
            PlayerIndex = ReadInt("UKSS_PLAYER_INDEX", 1, 1, 2),
            Width = ReadInt("UKSS_WINDOW_WIDTH", 960, 320, 16384),
            Height = ReadInt("UKSS_WINDOW_HEIGHT", 1080, 240, 16384),
            Muted = string.Equals(Environment.GetEnvironmentVariable("UKSS_MUTED"), "1", StringComparison.Ordinal)
        };
    }

    private static int ReadInt(string name, int fallback, int minimum, int maximum)
    {
        string? raw = Environment.GetEnvironmentVariable(name);
        return int.TryParse(raw, out int value)
            ? Math.Clamp(value, minimum, maximum)
            : fallback;
    }
}
