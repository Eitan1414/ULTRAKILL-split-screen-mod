using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ULTRAKILLSplitScreen.Launcher;

internal sealed class InstanceSandboxManager
{
    private const string SteamAppId = "1229490";

    private readonly string _sourceRoot;
    private readonly string _executableName;
    private readonly string _sessionRoot;

    public InstanceSandboxManager(string gameExecutable)
    {
        _sourceRoot = Path.GetDirectoryName(gameExecutable)
            ?? throw new InvalidOperationException("The game executable has no parent directory.");
        _executableName = Path.GetFileName(gameExecutable);

        string localRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ULTRAKILLSplitScreen",
            "Sessions");
        string sessionName = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Environment.ProcessId}";
        _sessionRoot = Path.Combine(localRoot, sessionName);
        Directory.CreateDirectory(_sessionRoot);
    }

    public InstanceLaunchPath Prepare(int playerIndex)
    {
        if (playerIndex <= 1)
        {
            string originalExecutable = Path.Combine(_sourceRoot, _executableName);
            string originalLog = Path.Combine(_sessionRoot, "Player1", "Player.log");
            Directory.CreateDirectory(Path.GetDirectoryName(originalLog)!);
            return new InstanceLaunchPath(originalExecutable, _sourceRoot, originalLog, false);
        }

        string playerRoot = Path.Combine(_sessionRoot, $"Player{playerIndex}");
        string logDirectory = Path.Combine(playerRoot, "Logs");
        Directory.CreateDirectory(playerRoot);
        Directory.CreateDirectory(logDirectory);

        MirrorTopLevelDirectories(playerRoot);
        MirrorTopLevelFiles(playerRoot);
        CopyBepInEx(playerRoot);

        File.WriteAllText(Path.Combine(playerRoot, "steam_appid.txt"), SteamAppId + Environment.NewLine);

        string executable = Path.Combine(playerRoot, _executableName);
        if (!File.Exists(executable))
            throw new FileNotFoundException("The isolated ULTRAKILL executable could not be prepared.", executable);

        return new InstanceLaunchPath(
            executable,
            playerRoot,
            Path.Combine(logDirectory, "Player.log"),
            true);
    }

    private void MirrorTopLevelDirectories(string playerRoot)
    {
        foreach (string sourceDirectory in Directory.EnumerateDirectories(_sourceRoot, "*", SearchOption.TopDirectoryOnly))
        {
            string name = Path.GetFileName(sourceDirectory);
            if (string.Equals(name, "BepInEx", StringComparison.OrdinalIgnoreCase))
                continue;

            string destination = Path.Combine(playerRoot, name);
            CreateDirectoryJunction(sourceDirectory, destination);
        }
    }

    private void MirrorTopLevelFiles(string playerRoot)
    {
        foreach (string sourceFile in Directory.EnumerateFiles(_sourceRoot, "*", SearchOption.TopDirectoryOnly))
        {
            string name = Path.GetFileName(sourceFile);
            if (ShouldSkipRootFile(name))
                continue;

            string destination = Path.Combine(playerRoot, name);
            LinkOrCopyFile(sourceFile, destination);
        }
    }

    private static bool ShouldSkipRootFile(string name)
    {
        return name.Equals("jaket-lobby-code.txt", StringComparison.OrdinalIgnoreCase)
            || name.Equals("splitscreen.json", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("ukss-ready-", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".log", StringComparison.OrdinalIgnoreCase)
            || name.Equals("ULTRAKILLSplitScreen.Launcher.exe", StringComparison.OrdinalIgnoreCase);
    }

    private void CopyBepInEx(string playerRoot)
    {
        string source = Path.Combine(_sourceRoot, "BepInEx");
        if (!Directory.Exists(source))
            return;

        string destination = Path.Combine(playerRoot, "BepInEx");
        CopyDirectory(source, destination);
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(source, directory);
            if (IsSkippedBepInExPath(relative))
                continue;
            Directory.CreateDirectory(Path.Combine(destination, relative));
        }

        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(source, file);
            if (IsSkippedBepInExPath(relative)
                || Path.GetFileName(file).EndsWith(".log", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }

    private static bool IsSkippedBepInExPath(string relativePath)
    {
        string normalized = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        return normalized.Equals("cache", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith($"cache{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
    }

    private static void CreateDirectoryJunction(string source, string destination)
    {
        if (Directory.Exists(destination))
            return;

        string commandProcessor = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
        var startInfo = new ProcessStartInfo
        {
            FileName = commandProcessor,
            Arguments = $"/d /c mklink /J \"{destination}\" \"{source}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the Windows junction command.");
        process.WaitForExit();

        if (process.ExitCode != 0 || !Directory.Exists(destination))
        {
            string details = process.StandardError.ReadToEnd();
            if (string.IsNullOrWhiteSpace(details))
                details = process.StandardOutput.ReadToEnd();
            throw new InvalidOperationException(
                $"Could not create the isolated directory junction '{destination}'. {details}".Trim());
        }
    }

    private static void LinkOrCopyFile(string source, string destination)
    {
        if (File.Exists(destination))
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (!CreateHardLink(destination, source, nint.Zero))
            File.Copy(source, destination, true);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string newFileName, string existingFileName, nint securityAttributes);
}

internal sealed record InstanceLaunchPath(
    string ExecutablePath,
    string WorkingDirectory,
    string UnityLogPath,
    bool IsIsolated);
