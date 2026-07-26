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

    [JsonPropertyName("players")]
    public int Players { get; set; } = 2;

    [JsonPropertyName("layout")]
    public string Layout { get; set; } = "auto";

    [JsonPropertyName("aspectMode")]
    public string AspectMode { get; set; } = "fit";

    [JsonPropertyName("targetAspectRatio")]
    public string TargetAspectRatio { get; set; } = "16:9";

    [JsonPropertyName("targetMonitor")]
    public int TargetMonitor { get; set; }

    [JsonPropertyName("windowGapPixels")]
    public int WindowGapPixels { get; set; } = 4;

    [JsonPropertyName("launchDelayMs")]
    public int LaunchDelayMs { get; set; } = 2500;

    [JsonPropertyName("windowReadyTimeoutMs")]
    public int WindowReadyTimeoutMs { get; set; } = 30000;

    [JsonPropertyName("borderless")]
    public bool Borderless { get; set; } = true;

    [JsonPropertyName("controllerIsolation")]
    public bool ControllerIsolation { get; set; } = true;

    [JsonPropertyName("controllerProfile")]
    public string ControllerProfile { get; set; } = "auto";

    [JsonPropertyName("controllerAssignments")]
    public int[] ControllerAssignments { get; set; } = [0, 1, 2, 3];

    [JsonPropertyName("mutedPlayers")]
    public int[] MutedPlayers { get; set; } = [];

    [JsonPropertyName("jaket")]
    public JaketConfig Jaket { get; set; } = new();

    [JsonPropertyName("extraArguments")]
    public string ExtraArguments { get; set; } = string.Empty;

    // Kept for compatibility with v0.1 configuration files.
    [JsonPropertyName("playerOneMuted")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool PlayerOneMuted { get; set; }

    [JsonPropertyName("playerTwoMuted")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool PlayerTwoMuted { get; set; }

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
        Players = Math.Clamp(Players, 1, 4);

        Layout = Layout.Trim().ToLowerInvariant() switch
        {
            "vertical" => "vertical",
            "horizontal" => "horizontal",
            "grid" => "grid",
            _ => "auto"
        };

        AspectMode = string.Equals(AspectMode, "stretch", StringComparison.OrdinalIgnoreCase)
            || string.Equals(AspectMode, "fill", StringComparison.OrdinalIgnoreCase)
            ? "stretch"
            : "fit";

        ControllerProfile = ControllerProfile.Trim().ToLowerInvariant() switch
        {
            "xbox" => "xbox",
            "playstation" => "playstation",
            "ps4" => "playstation",
            "ps5" => "playstation",
            "switch" => "switch",
            "nintendo" => "switch",
            _ => "auto"
        };

        if (!LayoutEngine.TryParseAspectRatio(TargetAspectRatio, out _))
            TargetAspectRatio = "16:9";

        TargetMonitor = Math.Clamp(TargetMonitor, 0, 15);
        WindowGapPixels = Math.Clamp(WindowGapPixels, 0, 64);
        LaunchDelayMs = Math.Clamp(LaunchDelayMs, 0, 60000);
        WindowReadyTimeoutMs = Math.Clamp(WindowReadyTimeoutMs, 5000, 120000);

        ControllerAssignments ??= [];
        if (ControllerAssignments.Length < Players)
        {
            int originalLength = ControllerAssignments.Length;
            int[] resizedAssignments = ControllerAssignments;
            Array.Resize(ref resizedAssignments, Players);
            for (int index = originalLength; index < Players; index++)
                resizedAssignments[index] = index;
            ControllerAssignments = resizedAssignments;
        }

        MutedPlayers ??= [];
        var muted = new HashSet<int>(MutedPlayers.Where(player => player >= 1 && player <= Players));
        if (PlayerOneMuted)
            muted.Add(1);
        if (PlayerTwoMuted && Players >= 2)
            muted.Add(2);
        MutedPlayers = muted.OrderBy(player => player).ToArray();

        Jaket ??= new JaketConfig();
        Jaket.Normalize(Players);
    }

    public bool IsMuted(int playerIndex) => MutedPlayers.Contains(playerIndex);

    public int ControllerFor(int playerIndex)
    {
        int arrayIndex = playerIndex - 1;
        return arrayIndex >= 0 && arrayIndex < ControllerAssignments.Length
            ? ControllerAssignments[arrayIndex]
            : arrayIndex;
    }
}

internal sealed class JaketConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("autoHostJoin")]
    public bool AutoHostJoin { get; set; } = true;

    [JsonPropertyName("hostPlayer")]
    public int HostPlayer { get; set; } = 1;

    [JsonPropertyName("lobbyCodeFile")]
    public string LobbyCodeFile { get; set; } = "jaket-lobby-code.txt";

    [JsonPropertyName("startDelaySeconds")]
    public int StartDelaySeconds { get; set; } = 8;

    [JsonPropertyName("timeoutSeconds")]
    public int TimeoutSeconds { get; set; } = 60;

    public void Normalize(int players)
    {
        HostPlayer = Math.Clamp(HostPlayer, 1, players);
        LobbyCodeFile = string.IsNullOrWhiteSpace(LobbyCodeFile)
            ? "jaket-lobby-code.txt"
            : LobbyCodeFile.Trim();
        StartDelaySeconds = Math.Clamp(StartDelaySeconds, 1, 120);
        TimeoutSeconds = Math.Clamp(TimeoutSeconds, 10, 300);
    }
}

internal readonly record struct WindowArea(int X, int Y, int Width, int Height);
internal readonly record struct PlayerWindow(int PlayerIndex, WindowArea Tile, WindowArea Content);