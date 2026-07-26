using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace ULTRAKILLSplitScreen.Launcher;

internal static class GameLocator
{
    private const string ExecutableName = "ULTRAKILL.exe";
    private const string SteamAppId = "1229490";

    public static string? Locate(string? configuredPath)
    {
        var candidates = new List<string>();

        AddCandidate(candidates, configuredPath);
        AddCandidate(candidates, Environment.GetEnvironmentVariable("ULTRAKILL_PATH"));
        AddRegistryCandidates(candidates);
        AddDefaultSteamCandidates(candidates);

        return candidates
            .Select(NormalizeExecutablePath)
            .FirstOrDefault(File.Exists);
    }

    private static void AddRegistryCandidates(List<string> candidates)
    {
        foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using RegistryKey? key = baseKey.OpenSubKey($"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Steam App {SteamAppId}");
                AddCandidate(candidates, key?.GetValue("InstallLocation") as string);
            }
            catch (Exception)
            {
                // Registry detection is best-effort; Steam library parsing runs afterwards.
            }
        }
    }

    private static void AddDefaultSteamCandidates(List<string> candidates)
    {
        var steamRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string? programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        if (!string.IsNullOrWhiteSpace(programFilesX86))
            steamRoots.Add(Path.Combine(programFilesX86, "Steam"));
        if (!string.IsNullOrWhiteSpace(programFiles))
            steamRoots.Add(Path.Combine(programFiles, "Steam"));

        foreach (string steamRoot in steamRoots.ToArray())
        {
            string libraryFile = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(libraryFile))
                continue;

            try
            {
                string contents = File.ReadAllText(libraryFile);
                foreach (Match match in Regex.Matches(contents, "\\\"path\\\"\\s+\\\"(?<path>[^\\\"]+)\\\"", RegexOptions.IgnoreCase))
                {
                    string decodedPath = match.Groups["path"].Value.Replace("\\\\", "\\", StringComparison.Ordinal);
                    if (!string.IsNullOrWhiteSpace(decodedPath))
                        steamRoots.Add(decodedPath);
                }
            }
            catch (IOException)
            {
                // Ignore unreadable Steam metadata and continue with known roots.
            }
        }

        foreach (string steamRoot in steamRoots)
        {
            AddCandidate(candidates, Path.Combine(steamRoot, "steamapps", "common", "ULTRAKILL"));
        }
    }

    private static void AddCandidate(List<string> candidates, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            candidates.Add(value.Trim().Trim('"'));
    }

    private static string NormalizeExecutablePath(string candidate)
    {
        string expanded = Environment.ExpandEnvironmentVariables(candidate);
        return string.Equals(Path.GetFileName(expanded), ExecutableName, StringComparison.OrdinalIgnoreCase)
            ? Path.GetFullPath(expanded)
            : Path.GetFullPath(Path.Combine(expanded, ExecutableName));
    }
}
