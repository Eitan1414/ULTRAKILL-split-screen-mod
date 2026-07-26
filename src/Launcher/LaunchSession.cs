using System.Diagnostics;

namespace ULTRAKILLSplitScreen.Launcher;

internal sealed class LaunchSession
{
    private readonly LauncherConfig _config;
    private readonly string _gameExecutable;
    private readonly string _lobbyCodePath;
    private readonly int? _attachedProcessId;
    private readonly string? _readyFilePath;
    private readonly InstanceSandboxManager _sandboxManager;

    public LaunchSession(
        LauncherConfig config,
        string gameExecutable,
        int? attachedProcessId = null,
        string? readyFilePath = null)
    {
        _config = config;
        _gameExecutable = gameExecutable;
        _attachedProcessId = attachedProcessId;
        _readyFilePath = string.IsNullOrWhiteSpace(readyFilePath) ? null : Path.GetFullPath(readyFilePath);
        _lobbyCodePath = ResolveLobbyCodePath(config.Jaket.LobbyCodeFile);
        _sandboxManager = new InstanceSandboxManager(gameExecutable);
    }

    public async Task RunAsync()
    {
        MonitorDisplay monitor = NativeWindow.GetMonitor(_config.TargetMonitor, out bool monitorFallback);
        PlayerWindow[] windows = LayoutEngine.Create(_config, monitor.Bounds);
        ValidateJaketInstallation();

        if (_attachedProcessId is null)
            PrepareLobbyCodeFile();

        Console.WriteLine($"Target monitor: #{monitor.Index + 1} {monitor.DeviceName} ({monitor.Bounds.Width}x{monitor.Bounds.Height})");
        if (monitorFallback)
            Console.WriteLine($"WARNING: monitor #{_config.TargetMonitor + 1} was not found; using the primary monitor.");
        Console.WriteLine($"Players: {_config.Players}");
        Console.WriteLine($"Layout: {_config.Layout}, aspect: {_config.AspectMode} {_config.TargetAspectRatio}");
        Console.WriteLine($"Controller profile: {_config.ControllerProfile}");

        var instances = new List<RunningInstance>(_config.Players);
        int firstPlayerToLaunch = 1;

        if (_attachedProcessId is int processId)
        {
            Process attached = Process.GetProcessById(processId);
            if (attached.HasExited)
                throw new InvalidOperationException($"The attached ULTRAKILL process {processId} has already exited.");

            PlayerWindow playerOneWindow = windows[0];
            nint attachedHandle = await NativeWindow.WaitForMainWindowAsync(attached, _config.WindowReadyTimeoutMs);
            NativeWindow.ApplyLayout(attachedHandle, playerOneWindow.Content, _config.Borderless);
            instances.Add(new RunningInstance(attached, attachedHandle, playerOneWindow.Content));
            firstPlayerToLaunch = 2;
            Console.WriteLine($"Attached the current solo process {processId} as player 1.");
        }

        foreach (PlayerWindow playerWindow in windows.Where(window => window.PlayerIndex >= firstPlayerToLaunch))
        {
            Console.WriteLine($"Preparing isolated runtime for player {playerWindow.PlayerIndex}...");
            Process process = StartPlayer(playerWindow);
            nint handle = await NativeWindow.WaitForMainWindowAsync(process, _config.WindowReadyTimeoutMs);
            NativeWindow.ApplyLayout(handle, playerWindow.Content, _config.Borderless);
            instances.Add(new RunningInstance(process, handle, playerWindow.Content));

            if (playerWindow.PlayerIndex < _config.Players && _config.LaunchDelayMs > 0)
                await Task.Delay(_config.LaunchDelayMs).ConfigureAwait(false);
        }

        WriteReadyFile("OK");

        // Unity can recreate or resize windows while loading. Re-resolve the window handle and reapply placements.
        for (int attempt = 0; attempt < 8; attempt++)
        {
            await Task.Delay(1000).ConfigureAwait(false);
            foreach (RunningInstance instance in instances)
            {
                if (instance.Process.HasExited)
                    continue;

                instance.Process.Refresh();
                nint currentHandle = instance.Process.MainWindowHandle;
                if (currentHandle != nint.Zero)
                    NativeWindow.ApplyLayout(currentHandle, instance.Area, _config.Borderless);
            }
        }

        Console.WriteLine();
        Console.WriteLine($"{instances.Count} ULTRAKILL instances are arranged on monitor #{monitor.Index + 1}.");
        Console.WriteLine("Additional players use isolated runtime folders and separate Unity/BepInEx logs.");
        Console.WriteLine(_config.ControllerIsolation
            ? "Unity Input System controller isolation is enabled."
            : "Controller isolation is disabled; every instance may see every controller.");
        Console.WriteLine(_config.Jaket.Enabled && _config.Jaket.AutoHostJoin
            ? "Jaket auto host/join is enabled. Check each isolated BepInEx log for the lobby result."
            : "Jaket auto host/join is disabled.");
    }

    public void ReportFailure(Exception exception)
    {
        WriteReadyFile($"ERROR:{exception.Message}");
    }

    private Process StartPlayer(PlayerWindow playerWindow)
    {
        int playerIndex = playerWindow.PlayerIndex;
        WindowArea area = playerWindow.Content;
        InstanceLaunchPath runtime = _sandboxManager.Prepare(playerIndex);

        string arguments = string.Join(" ", new[]
        {
            "-screen-fullscreen 0",
            $"-screen-width {area.Width}",
            $"-screen-height {area.Height}",
            "-popupwindow",
            $"-logFile {Quote(runtime.UnityLogPath)}",
            _config.ExtraArguments.Trim()
        }.Where(value => !string.IsNullOrWhiteSpace(value)));

        var startInfo = new ProcessStartInfo
        {
            FileName = runtime.ExecutablePath,
            WorkingDirectory = runtime.WorkingDirectory,
            Arguments = arguments,
            UseShellExecute = false
        };

        startInfo.Environment["SteamAppId"] = "1229490";
        startInfo.Environment["SteamGameId"] = "1229490";
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
        startInfo.Environment["UKSS_GAMEPAD_PROFILE"] = _config.ControllerProfile;
        startInfo.Environment["UKSS_JAKET_ENABLED"] = _config.Jaket.Enabled && _config.Jaket.AutoHostJoin ? "1" : "0";
        startInfo.Environment["UKSS_JAKET_HOST"] = playerIndex == _config.Jaket.HostPlayer ? "1" : "0";
        startInfo.Environment["UKSS_JAKET_CODE_FILE"] = _lobbyCodePath;
        startInfo.Environment["UKSS_JAKET_START_DELAY"] = _config.Jaket.StartDelaySeconds.ToString();
        startInfo.Environment["UKSS_JAKET_TIMEOUT"] = _config.Jaket.TimeoutSeconds.ToString();
        startInfo.Environment["UKSS_SAFE_INSTANCE"] = runtime.IsIsolated ? "1" : "0";

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

    private void WriteReadyFile(string value)
    {
        if (_readyFilePath is null)
            return;

        try
        {
            string? directory = Path.GetDirectoryName(_readyFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(_readyFilePath, value + Environment.NewLine);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"WARNING: could not write the startup handshake file: {exception.Message}");
        }
    }

    private static string ResolveLobbyCodePath(string configuredPath)
    {
        string expanded = Environment.ExpandEnvironmentVariables(configuredPath);
        return Path.IsPathRooted(expanded)
            ? Path.GetFullPath(expanded)
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, expanded));
    }

    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";

    private sealed record RunningInstance(Process Process, nint Handle, WindowArea Area);
}
