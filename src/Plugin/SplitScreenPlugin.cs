using System.Collections;
using System.Diagnostics;
using BepInEx;
using UnityEngine;

namespace ULTRAKILLSplitScreen.Plugin;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInProcess("ULTRAKILL.exe")]
[BepInDependency("xzxADIxzx.Jaket", BepInDependency.DependencyFlags.SoftDependency)]
public sealed class SplitScreenPlugin : BaseUnityPlugin
{
    private const string PluginGuid = "eitan1414.ultrakill.splitscreen";
    private const string PluginName = "ULTRAKILL Split-Screen Bridge";
    private const string PluginVersion = "0.3.1";

    private InstanceSettings _settings = null!;
    private HotkeySplitScreenMenu _menu = null!;
    private bool _isolationStarted;
    private bool _jaketStarted;
    private bool _cameraAspectStarted;
    private bool _hotkeyLaunchInProgress;

    private void Awake()
    {
        _settings = InstanceSettings.FromEnvironment();
        _menu = new HotkeySplitScreenMenu(StartHotkeySession);
        Application.runInBackground = true;

        if (_settings.Muted)
            AudioListener.volume = 0f;

        Logger.LogInfo($"Loaded split-screen bridge v{PluginVersion} for player {_settings.PlayerIndex}/{_settings.PlayerCount}.");
        Logger.LogInfo("While playing solo, press Ctrl+P to open the split-screen popup.");

        if (_settings.ManagedWindow)
        {
            StartCoroutine(ReapplyWindowSettings());
            StartCameraAspectCorrection();
        }

        if (_settings.InputIsolation)
            StartGamepadIsolation();

        if (_settings.JaketEnabled)
            StartJaketAutomation();
    }

    private void Update()
    {
        if (_settings.PlayerCount != 1 || _hotkeyLaunchInProgress)
            return;

        bool control = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        if (control && Input.GetKeyDown(KeyCode.P))
            _menu.Toggle();
    }

    private void OnGUI()
    {
        _menu.Draw();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            Application.runInBackground = true;
    }

    private string? StartHotkeySession(HotkeyLaunchRequest request)
    {
        if (_hotkeyLaunchInProgress)
            return "Un démarrage du split-screen est déjà en cours.";

        string? launcherPath = FindLauncher();
        if (launcherPath is null)
        {
            return "ULTRAKILLSplitScreen.Launcher.exe est introuvable. Extrais tout le ZIP directement dans le dossier d’ULTRAKILL.";
        }

        try
        {
            int currentProcessId = Process.GetCurrentProcess().Id;
            string lobbyCodePath = Path.Combine(Paths.GameRootPath, "jaket-lobby-code.txt");
            string readyFilePath = Path.Combine(Paths.GameRootPath, $"ukss-ready-{currentProcessId}.flag");
            SafeDelete(lobbyCodePath);
            SafeDelete(readyFilePath);

            _hotkeyLaunchInProgress = true;
            StartCoroutine(BeginHotkeySession(
                request,
                launcherPath,
                lobbyCodePath,
                readyFilePath,
                currentProcessId));
            return null;
        }
        catch (Exception exception)
        {
            _hotkeyLaunchInProgress = false;
            Logger.LogError(exception);
            return $"Impossible de préparer le split-screen : {exception.Message}";
        }
    }

    private IEnumerator BeginHotkeySession(
        HotkeyLaunchRequest request,
        string launcherPath,
        string lobbyCodePath,
        string readyFilePath,
        int currentProcessId)
    {
        // Moving an exclusive-fullscreen Unity window through Win32 can be unstable.
        // Switch the current solo process to a normal window before the launcher attaches to it.
        Screen.fullScreenMode = FullScreenMode.Windowed;
        yield return null;
        yield return new WaitForSecondsRealtime(0.75f);

        string arguments = string.Join(" ", new[]
        {
            $"--attach-pid {currentProcessId}",
            $"--players {request.TotalPlayers}",
            $"--controller-profile {request.ControllerProfile}",
            $"--monitor {request.TargetMonitor + 1}",
            $"--ready-file {Quote(readyFilePath)}",
            request.FillScreen ? "--fill-screen" : string.Empty
        }.Where(argument => !string.IsNullOrWhiteSpace(argument)));

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = launcherPath,
                WorkingDirectory = Paths.GameRootPath,
                Arguments = arguments,
                UseShellExecute = false
            };

            Process.Start(startInfo);
            Logger.LogInfo($"Ctrl+P requested {request.TotalPlayers}-player split-screen on monitor #{request.TargetMonitor + 1}.");
        }
        catch (Exception exception)
        {
            _hotkeyLaunchInProgress = false;
            Logger.LogError($"Could not start the split-screen launcher: {exception}");
            yield break;
        }

        float deadline = Time.realtimeSinceStartup + 90f;
        string result = string.Empty;
        while (Time.realtimeSinceStartup < deadline)
        {
            try
            {
                if (File.Exists(readyFilePath))
                {
                    result = File.ReadAllText(readyFilePath).Trim();
                    if (!string.IsNullOrWhiteSpace(result))
                        break;
                }
            }
            catch (IOException)
            {
                // The launcher may still be replacing the handshake file.
            }

            yield return new WaitForSecondsRealtime(0.25f);
        }

        SafeDelete(readyFilePath);

        if (!string.Equals(result, "OK", StringComparison.OrdinalIgnoreCase))
        {
            _hotkeyLaunchInProgress = false;
            string reason = result.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase)
                ? result.Substring("ERROR:".Length)
                : "le launcher n’a pas confirmé le démarrage dans les 90 secondes";
            Logger.LogError($"Split-screen startup cancelled: {reason}");
            yield break;
        }

        // Only touch the current player after all additional game windows successfully exist.
        _settings.ConfigureHotkeySession(request.TotalPlayers, request.ControllerProfile, lobbyCodePath);
        StartGamepadIsolation();
        StartCameraAspectCorrection();
        StartJaketAutomation();
        _hotkeyLaunchInProgress = false;

        Logger.LogInfo("Additional players are ready; enabled player-1 mapping and Jaket automation.");
    }

    private string? FindLauncher()
    {
        string[] candidates =
        [
            Path.Combine(Paths.GameRootPath, "ULTRAKILLSplitScreen.Launcher.exe"),
            Path.Combine(AppContext.BaseDirectory, "ULTRAKILLSplitScreen.Launcher.exe")
        ];

        return candidates.FirstOrDefault(File.Exists);
    }

    private void StartGamepadIsolation()
    {
        if (_isolationStarted)
            return;
        _isolationStarted = true;
        StartCoroutine(MaintainGamepadIsolation());
    }

    private void StartJaketAutomation()
    {
        if (_jaketStarted || !_settings.JaketEnabled)
            return;
        _jaketStarted = true;
        StartCoroutine(JaketBridge.Run(
            _settings,
            message => Logger.LogInfo(message),
            message => Logger.LogWarning(message)));
    }

    private void StartCameraAspectCorrection()
    {
        if (_cameraAspectStarted)
            return;
        _cameraAspectStarted = true;
        StartCoroutine(MaintainCameraAspect());
    }

    private IEnumerator ReapplyWindowSettings()
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            Application.runInBackground = true;
            Screen.fullScreenMode = FullScreenMode.Windowed;
            Screen.SetResolution(_settings.Width, _settings.Height, false);
            yield return new WaitForSecondsRealtime(0.5f);
        }
    }

    private IEnumerator MaintainCameraAspect()
    {
        while (true)
        {
            if (_settings.PlayerCount > 1 && Screen.height > 0)
            {
                float aspect = Screen.width / (float)Screen.height;
                foreach (Camera camera in Camera.allCameras)
                {
                    if (camera != null && camera.targetTexture == null)
                        camera.aspect = aspect;
                }
            }
            yield return new WaitForSecondsRealtime(0.5f);
        }
    }

    private IEnumerator MaintainGamepadIsolation()
    {
        string previousMessage = string.Empty;
        while (true)
        {
            bool success = false;
            string message;
            try
            {
                success = GamepadIsolation.TryApply(
                    _settings.GamepadIndex,
                    _settings.GamepadProfile,
                    out message);
            }
            catch (Exception exception)
            {
                message = $"Controller isolation error: {exception.Message}";
            }

            if (!string.Equals(message, previousMessage, StringComparison.Ordinal))
            {
                if (success)
                    Logger.LogInfo(message);
                else
                    Logger.LogWarning(message);
                previousMessage = message;
            }

            yield return new WaitForSecondsRealtime(success ? 5f : 1f);
        }
    }

    private static void SafeDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
            // A stale file must not crash the running solo game.
        }
        catch (UnauthorizedAccessException)
        {
            // The launcher will report a clearer error if it cannot write the file later.
        }
    }

    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";
}
