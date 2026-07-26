using System.Diagnostics;

namespace ULTRAKILLSplitScreen.Launcher;

internal sealed class LaunchSession
{
    private readonly LauncherConfig _config;
    private readonly string _gameExecutable;

    public LaunchSession(LauncherConfig config, string gameExecutable)
    {
        _config = config;
        _gameExecutable = gameExecutable;
    }

    public async Task RunAsync()
    {
        WindowArea[] areas = CreateAreas(_config.Layout);

        Console.WriteLine($"Screen: {NativeWindow.ScreenWidth}x{NativeWindow.ScreenHeight}");
        Console.WriteLine($"Layout: {_config.Layout}");
        Console.WriteLine("Launching player 1...");

        Process playerOne = StartPlayer(1, areas[0], _config.PlayerOneMuted);
        nint playerOneWindow = await NativeWindow.WaitForMainWindowAsync(playerOne, _config.WindowReadyTimeoutMs);
        NativeWindow.ApplyLayout(playerOneWindow, areas[0], _config.Borderless);

        if (_config.LaunchDelayMs > 0)
            await Task.Delay(_config.LaunchDelayMs).ConfigureAwait(false);

        Console.WriteLine("Launching player 2...");
        Process playerTwo = StartPlayer(2, areas[1], _config.PlayerTwoMuted);
        nint playerTwoWindow = await NativeWindow.WaitForMainWindowAsync(playerTwo, _config.WindowReadyTimeoutMs);
        NativeWindow.ApplyLayout(playerTwoWindow, areas[1], _config.Borderless);

        // Unity can recreate or resize its window during startup. Reapply the layout a few times.
        for (int attempt = 0; attempt < 4; attempt++)
        {
            await Task.Delay(1000).ConfigureAwait(false);
            if (!playerOne.HasExited)
                NativeWindow.ApplyLayout(playerOneWindow, areas[0], _config.Borderless);
            if (!playerTwo.HasExited)
                NativeWindow.ApplyLayout(playerTwoWindow, areas[1], _config.Borderless);
        }

        Console.WriteLine();
        Console.WriteLine("Both ULTRAKILL instances are running.");
        Console.WriteLine("Version 0.1 does not yet isolate controllers or join a Jaket lobby automatically.");
    }

    private Process StartPlayer(int playerIndex, WindowArea area, bool muted)
    {
        string workingDirectory = Path.GetDirectoryName(_gameExecutable)
            ?? throw new InvalidOperationException("The game executable has no parent directory.");

        string arguments = string.Join(' ', new[]
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
        startInfo.Environment["UKSS_WINDOW_X"] = area.X.ToString();
        startInfo.Environment["UKSS_WINDOW_Y"] = area.Y.ToString();
        startInfo.Environment["UKSS_WINDOW_WIDTH"] = area.Width.ToString();
        startInfo.Environment["UKSS_WINDOW_HEIGHT"] = area.Height.ToString();
        startInfo.Environment["UKSS_LAYOUT"] = _config.Layout;
        startInfo.Environment["UKSS_MUTED"] = muted ? "1" : "0";

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Unable to launch ULTRAKILL for player {playerIndex}.");
    }

    private static WindowArea[] CreateAreas(string layout)
    {
        int screenWidth = NativeWindow.ScreenWidth;
        int screenHeight = NativeWindow.ScreenHeight;

        if (screenWidth <= 0 || screenHeight <= 0)
            throw new InvalidOperationException("Windows returned an invalid primary-screen resolution.");

        if (string.Equals(layout, "horizontal", StringComparison.OrdinalIgnoreCase))
        {
            int topHeight = screenHeight / 2;
            return
            [
                new WindowArea(0, 0, screenWidth, topHeight),
                new WindowArea(0, topHeight, screenWidth, screenHeight - topHeight)
            ];
        }

        int leftWidth = screenWidth / 2;
        return
        [
            new WindowArea(0, 0, leftWidth, screenHeight),
            new WindowArea(leftWidth, 0, screenWidth - leftWidth, screenHeight)
        ];
    }
}
