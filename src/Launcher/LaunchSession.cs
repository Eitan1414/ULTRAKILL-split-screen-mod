using System.Diagnostics;

namespace ULTRAKILLSplitScreen.Launcher;

internal sealed class LaunchSession
{
    private readonly LauncherConfig _config;
    private readonly string _gameExecutable;
    private readonly string _lobbyCodePath;

    public LaunchSession(LauncherConfig config, string gameExecutable)
    {
        _config = config;
        _gameExecutable = gameExecutable;
        _lobbyCodePath = ResolveLobbyCodePath(config.Jaket.LobbyCodeFile);
    }

    public async Task RunAsync()
    {
        PlayerWindow[] windows = LayoutEngine.Create(_config);
        ValidateJaketInstallation();
        PrepareLobbyCodeFile();

        Console.WriteLine($"Screen: {NativeWindow.ScreenWidth}x{NativeWindow.ScreenHeight}");
        Console.WriteLine($"Players: {_config.Players}");
        Console.WriteLine($"Layout: {_config.Layout}, aspect: {_config.AspectMode} {_config.TargetAspectRatio}");

        var instances = new List<RunningInstance>(_config.Players);
        foreach (PlayerWindow playerWindow in windows)
        {
            Console.WriteLine($"Launching player {playerWindow.PlayerIndex} with gamepad #{_config.ControllerFor(playerWindow.PlayerIndex)}...");
            Process process = StartPlayer(playerWindow);
            nint handle = await NativeWindow.WaitForMainWindowAsync(process, _config.WindowReadyTimeoutMs);
            NativeWindow.ApplyLayout(handle, playerWindow.Content, _config.Borderless);
            instances.Add(new RunningInstance(process, handle, playerWindow.Content));

            if (playerWindow.PlayerIndex < _config.Players && _config.LaunchDelayMs > 0)
                await Task.Delay(_config.LaunchDelayMs).ConfigureAwait(false);
        }

        // Unity can recreate or resize windows while loading. Reapply all placements several times.
        for (int attempt = 0; attempt < 6; attempt++)
        {
            await Task.Delay(1000).ConfigureAwait(false);
            foreach (RunningInstance instance in instances)
            {
                if (!instance.Process.HasExited)
                    NativeWindow.ApplyLayout(instance.Handle, instance.Area, _config.Borderless);
            }
        }

        Console.WriteLine();
        Console.WriteLine($"{instances.Count} ULTRAKILL instances are running.");
        Console.WriteLine(_config.ControllerIsolation
            ? "Unity Input System controller isolation is enabled."
            : "Controller isolation is disabled; every instance may see every controller.");
        Console.WriteLine(_config.Jaket.Enabled && _config.Jaket.AutoHostJoin
            ? "Jaket auto host/join is enabled. Check BepInEx logs for the lobby result."
            : "Jaket auto host/join is disabled.");
    }

    private Process StartPlayer(PlayerWindow playerWindow)
    {
        int playerIndex = playerWindow.PlayerIndex;
        WindowArea area = playerWindow.Content;
        string workingDirectory = Path.GetDirectoryName(_gameExecutable)
            ?? throw new InvalidOperationException("The game executable has no parent directory.");

        string arguments = string.Join(" ", new[]
        {
            "-screen-fullscreen 0",
            $"-screen-width {area.Width}",
            $"-screen-height {area.Height}",
            "-popupwindow",
            _config.ExtraArguments.Trim()
        }.Where(value => !string.IsNullOrWhiteSpace(value)));

        var startInfo = new ProcessStartInfo
        {
            FileName = _gameExecutable,
            WorkingDirectory = workingDirectory,
            Arguments = arguments,
            UseShellExecute = false
        };

        startInfo.Environment["UKSS_PLAYER_INDEX"] = playerIndex.ToString();
        startInfo.Environment["UKSS_PLAYER_COUNT"] = _config.Players.ToString();
        startInfo.Environment["UKSS_WINDOW_X"] = area.X.ToString();
        startInfo.Environment["UKSS_WINDOW_Y"] = area.Y.ToString();
        startInfo.Environment["UKSS_WINDOW_WIDTH"] = area.Width.ToString();
        startInfo.Environment["UKSS_WINDOW_HEIGHT"] = area.Height.ToString();
        startInfo.Environment["UKSS_LAYOUT"] = _config.Layout;
        startInfo.Environment["UKSS_MUTED"] = _config.IsMuted(playerIndex) ? "1" : "0";
        startInfo.Environment["UKSS_INPUT_ISOLATION"] = _config.ControllerIsolation ? "1" : "0";
        startInfo.Environment["UKSS_GAMEPAD_INDEX"] = _config.ControllerFor(playerIndex).ToString();
        startInfo.Environment["UKSS_JAKET_ENABLED"] = _config.Jaket.Enabled && _config.Jaket.AutoHostJoin ? "1" : "0";
        startInfo.Environment["UKSS_JAKET_HOST"] = playerIndex == _config.Jaket.HostPlayer ? "1" : "0";
        startInfo.Environment["UKSS_JAKET_CODE_FILE"] = _lobbyCodePath;
        startInfo.Environment["UKSS_JAKET_START_DELAY"] = _config.Jaket.StartDelaySeconds.ToString();
        startInfo.Environment["UKSS_JAKET_TIMEOUT"] = _config.Jaket.TimeoutSeconds.ToString();

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Unable to launch ULTRAKILL for player {playerIndex}.");
    }

    private void ValidateJaketInstallation()
    {
        if (!_config.Jaket.Enabled)
            return;

        string gameDirectory = Path.GetDirectoryName(_gameExecutable) ?? string.Empty;
        string pluginDirectory = Path.Combine(gameDirectory, "BepInEx", "plugins");
        bool found = Directory.Exists(pluginDirectory)
            && Directory.EnumerateFiles(pluginDirectory, "Jaket.dll", SearchOption.AllDirectories).Any();

        if (!found)
            Console.WriteLine("WARNING: Jaket.dll was not found under BepInEx/plugins. Automatic co-op will be skipped by the plugin.");
    }

    private void PrepareLobbyCodeFile()
    {
        if (!_config.Jaket.Enabled || !_config.Jaket.AutoHostJoin)
            return;

        string? directory = Path.GetDirectoryName(_lobbyCodePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        if (File.Exists(_lobbyCodePath))
            File.Delete(_lobbyCodePath);
    }

    private static string ResolveLobbyCodePath(string configuredPath)
    {
        string expanded = Environment.ExpandEnvironmentVariables(configuredPath);
        return Path.IsPathRooted(expanded)
            ? Path.GetFullPath(expanded)
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, expanded));
    }

    private sealed record RunningInstance(Process Process, nint Handle, WindowArea Area);
}
