using System.Text;

namespace ULTRAKILLSplitScreen.Launcher;

internal static class Program
{
    [STAThread]
    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.Title = "ULTRAKILL Split-Screen Launcher";

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
            bool dryRun = args.Any(argument => string.Equals(argument, "--dry-run", StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(gameOverride))
                config.GameExecutable = gameOverride;
            if (!string.IsNullOrWhiteSpace(layoutOverride))
                config.Layout = layoutOverride;
            if (int.TryParse(playersOverride, out int playerCount))
                config.Players = playerCount;
            if (!string.IsNullOrWhiteSpace(aspectOverride))
                config.AspectMode = aspectOverride;

            config.Normalize();

            string? gameExecutable = GameLocator.Locate(config.GameExecutable);
            if (gameExecutable is null)
            {
                Console.Error.WriteLine("ULTRAKILL.exe was not found.");
                Console.Error.WriteLine($"Edit this file and set gameExecutable: {configPath}");
                return 2;
            }

            Console.WriteLine("ULTRAKILL Split-Screen Launcher v0.2.0");
            Console.WriteLine($"Game: {gameExecutable}");
            Console.WriteLine($"Config: {configPath}");
            PrintLayoutPreview(config);

            if (dryRun)
            {
                Console.WriteLine("Dry run completed; no game process was launched.");
                return 0;
            }

            var session = new LaunchSession(config, gameExecutable);
            await session.RunAsync().ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("The split-screen launcher stopped because of an error:");
            Console.Error.WriteLine(exception.Message);
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

    private static void PrintLayoutPreview(LauncherConfig config)
    {
        Console.WriteLine($"Players: {config.Players}");
        Console.WriteLine($"Layout: {config.Layout}");
        Console.WriteLine($"Aspect: {config.AspectMode} ({config.TargetAspectRatio})");
        Console.WriteLine($"Gamepads: {string.Join(", ", Enumerable.Range(1, config.Players).Select(player => $"P{player}=#{config.ControllerFor(player)}"))}");
        Console.WriteLine($"Jaket automation: {(config.Jaket.Enabled && config.Jaket.AutoHostJoin ? "enabled" : "disabled")}");
    }

    private static void PrintHelp()
    {
        Console.WriteLine("ULTRAKILL Split-Screen Launcher");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --game <path>          Path to ULTRAKILL.exe or its folder");
        Console.WriteLine("  --players <1-4>        Number of local instances");
        Console.WriteLine("  --layout <layout>      auto, vertical, horizontal or grid");
        Console.WriteLine("  --aspect-mode <mode>   fit (no stretching) or stretch");
        Console.WriteLine("  --dry-run              Detect and validate without launching");
        Console.WriteLine("  --help                 Show this help");
    }
}
