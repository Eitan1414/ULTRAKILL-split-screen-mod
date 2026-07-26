using System.Text.Json;
using System.Text.Json.Serialization;

namespace ULTRAKILLSplitScreen.Launcher;

internal sealed class LauncherConfig
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    [JsonPropertyName("gameExecutable")]
    public string GameExecutable { get; set; } = string.Empty;

    [JsonPropertyName("layout")]
    public string Layout { get; set; } = "vertical";

    [JsonPropertyName("launchDelayMs")]
    public int LaunchDelayMs { get; set; } = 3500;

    [JsonPropertyName("windowReadyTimeoutMs")]
    public int WindowReadyTimeoutMs { get; set; } = 30000;

    [JsonPropertyName("borderless")]
    public bool Borderless { get; set; } = true;

    [JsonPropertyName("playerOneMuted")]
    public bool PlayerOneMuted { get; set; }

    [JsonPropertyName("playerTwoMuted")]
    public bool PlayerTwoMuted { get; set; }

    [JsonPropertyName("extraArguments")]
    public string ExtraArguments { get; set; } = string.Empty;

    public static LauncherConfig LoadOrCreate(string path)
    {
        if (!File.Exists(path))
        {
            var created = new LauncherConfig();
            created.Save(path);
            return created;
        }

        string json = File.ReadAllText(path);
        LauncherConfig? config = JsonSerializer.Deserialize<LauncherConfig>(json, JsonOptions);
        return config ?? throw new InvalidDataException($"Invalid configuration file: {path}");
    }

    public void Save(string path)
    {
        string json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(path, json + Environment.NewLine);
    }

    public void Normalize()
    {
        Layout = string.Equals(Layout, "horizontal", StringComparison.OrdinalIgnoreCase)
            ? "horizontal"
            : "vertical";
        LaunchDelayMs = Math.Clamp(LaunchDelayMs, 0, 60000);
        WindowReadyTimeoutMs = Math.Clamp(WindowReadyTimeoutMs, 5000, 120000);
    }
}

internal readonly record struct WindowArea(int X, int Y, int Width, int Height);
