using System.Text;

namespace ULTRAKILLSplitScreen.Launcher;

internal static class Program
{
    [STAThread]
    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.Title = "ULTRAKILL Split-Screen Launcher";

        string? readyFilePath = ReadOption(args, "--ready-file");
        LaunchSession? session = null;

        try
        {
            if (args.Any(argument => string.Equals(argument, "--help", StringComparison.OrdinalIgnoreCase)))
            {
                PrintHelp();
                return 0;
            }

            string configPath = Path.Combine(AppContext.BaseDirectory, "splitscreen.json");
            LauncherConfig config = LauncherConfig.LoadOrCreate(configPath);

            string? gameOverride = ReadOption(args, "--game");
            string? layoutOverride = ReadOption(args, "--layout");
            string? playersOverride = ReadOption(args, "--players");
            string? aspectOverride = ReadOption(args, "--aspect-mode");
            string? monitorOverride = ReadOption(args, "--monitor");
            string? profileOverride = ReadOption(args, "--controller-profile");
            string? attachedPidRaw = ReadOption(args, "--attach-pid");
            bool fillScreen = HasFlag(args, "--fill-screen");
            bool dryRun = HasFlag(args, "--dry-run");

            if (!string.IsNullOrWhiteSpace(gameOverride))
                config.GameExecutable = gameOverride;
            if (!string.IsNullOrWhiteSpace(layoutOverride))
                config.Layout = layoutOverride;
            if (int.TryParse(playersOverride, out int playerCount))
                config.Players = playerCount;
            if (!string.IsNullOrWhiteSpace(aspectOverride))
                config.AspectMode = aspectOverride;
            if (int.TryParse(monitorOverride, out int monitorNumber))
                config.TargetMonitor = Math.Max(0, monitorNumber - 1);
            if (!string.IsNullOrWhiteSpace(profileOverride))
                config.ControllerProfile = profileOverride;
            if (fillScreen)
                config.AspectMode = "stretch";

            int? attachedProcessId = int.TryParse(attachedPidRaw, out int parsedPid) && parsedPid > 0
                ? parsedPid
                : null;

            config.Normalize();

            string? gameExecutable = GameLocator.Locate(config.GameExecutable);
            if (gameExecutable is null)
            {
                Console.Error.WriteLine("ULTRAKILL.exe was not found.");
                Console.Error.WriteLine($"Edit this file and set gameExecutable: {configPath}");
                WriteHandshake(readyFilePath, "ERROR:ULTRAKILL.exe was not found.");
                return 2;
            }

            Console.WriteLine("ULTRAKILL Split-Screen Launcher v0.3.1");
            Console.WriteLine($"Game: {gameExecutable}");
            Console.WriteLine($"Config: {configPath}");
            if (attachedProcessId is int attachPid)
                Console.WriteLine($"Attach mode: existing solo process {attachPid}");
            PrintLayoutPreview(config);

            if (dryRun)
            {
                Console.WriteLine("Dry run completed; no game process was launched.");
                WriteHandshake(readyFilePath, "OK");
                return 0;
            }

            session = new LaunchSession(config, gameExecutable, attachedProcessId, readyFilePath);
            await session.RunAsync().ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception)
        {
            session?.ReportFailure(exception);
            WriteHandshake(readyFilePath, $"ERROR:{exception.Message}");

            Console.Error.WriteLine();
            Console.Error.WriteLine("The split-screen launcher stopped because of an error:");
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static string? ReadOption(string[] args, string optionName)
    {
        for (int index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], optionName, StringComparison.OrdinalIgnoreCase))
                return args[index + 1];
        }

        return null;
    }

    private static bool HasFlag(string[] args, string flag)
    {
        return args.Any(argument => string.Equals(argument, flag, StringComparison.OrdinalIgnoreCase));
    }

    private static void WriteHandshake(string? path, string value)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            string fullPath = Path.GetFullPath(path);
            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(fullPath, value + Environment.NewLine);
        }
        catch
        {
            // The launcher already reports the main error through stderr.
        }
    }

    private static void PrintLayoutPreview(LauncherConfig config)
    {
        IReadOnlyList<MonitorDisplay> monitors = NativeWindow.GetMonitors();
        Console.WriteLine($"Detected monitors: {string.Join(", ", monitors.Select(monitor => $"#{monitor.Index + 1} {monitor.Bounds.Width}x{monitor.Bounds.Height}"))}");
        Console.WriteLine($"Target monitor: #{config.TargetMonitor + 1}");
        Console.WriteLine($"Players: {config.Players}");
        Console.WriteLine($"Layout: {config.Layout}");
        Console.WriteLine($"Aspect: {config.AspectMode} ({config.TargetAspectRatio})");
        Console.WriteLine($"Controller profile: {config.ControllerProfile}");
        Console.WriteLine($"Gamepads: {string.Join(", ", Enumerable.Range(1, config.Players).Select(player => $"P{player}=#{config.ControllerFor(player)}"))}");
        Console.WriteLine($"Jaket automation: {(config.Jaket.Enabled && config.Jaket.AutoHostJoin ? "enabled" : "disabled")}");
    }

    private static void PrintHelp()
    {
        Console.WriteLine("ULTRAKILL Split-Screen Launcher");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --game <path>                Path to ULTRAKILL.exe or its folder");
        Console.WriteLine("  --players <1-4>              Total number of local instances");
        Console.WriteLine("  --layout <layout>            auto, vertical, horizontal or grid");
        Console.WriteLine("  --aspect-mode <mode>         fit (16:9) or stretch/fill");
        Console.WriteLine("  --monitor <1-N>              Monitor number, where 1 is primary");
        Console.WriteLine("  --controller-profile <type>  auto, xbox, playstation or switch");
        Console.WriteLine("  --attach-pid <pid>           Reuse an already-running solo ULTRAKILL as player 1");
        Console.WriteLine("  --ready-file <path>          Write OK or ERROR after additional players start");
        Console.WriteLine("  --fill-screen                Fill every tile of the target monitor");
        Console.WriteLine("  --dry-run                    Detect and validate without launching");
        Console.WriteLine("  --help                       Show this help");
    }
}
