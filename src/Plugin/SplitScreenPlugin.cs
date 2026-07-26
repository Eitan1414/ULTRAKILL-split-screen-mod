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
    private const string PluginVersion = "0.3.0";

    private InstanceSettings _settings = null!;
    private HotkeySplitScreenMenu _menu = null!;
    private bool _isolationStarted;
    private bool _jaketStarted;
    private bool _cameraAspectStarted;

    private void Awake()
    {
        _settings = InstanceSettings.FromEnvironment();
        _menu = new HotkeySplitScreenMenu(StartHotkeySession);
        Application.runInBackground = true;

        if (_settings.Muted)
            AudioListener.volume = 0f;

        Logger.LogInfo($"Loaded split-screen bridge for player {_settings.PlayerIndex}/{_settings.PlayerCount}.");
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
        if (_settings.PlayerCount != 1)
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
        try
        {
            string? launcherPath = FindLauncher();
            if (launcherPath is null)
            {
                return "ULTRAKILLSplitScreen.Launcher.exe est introuvable. Extrais tout le ZIP v0.3 directement dans le dossier d’ULTRAKILL.";
            }

            string lobbyCodePath = Path.Combine(Paths.GameRootPath, "jaket-lobby-code.txt");
            if (File.Exists(lobbyCodePath))
                File.Delete(lobbyCodePath);

            _settings.ConfigureHotkeySession(request.TotalPlayers, request.ControllerProfile, lobbyCodePath);
            StartGamepadIsolation();
            StartCameraAspectCorrection();
            StartJaketAutomation();

            int currentProcessId = Process.GetCurrentProcess().Id;
            string arguments = string.Join(" ", new[]
            {
                $"--attach-pid {currentProcessId}",
                $"--players {request.TotalPlayers}",
                $"--controller-profile {request.ControllerProfile}",
                $"--monitor {request.TargetMonitor + 1}",
                request.FillScreen ? "--fill-screen" : string.Empty
            }.Where(argument => !string.IsNullOrWhiteSpace(argument)));

            var startInfo = new ProcessStartInfo
            {
                FileName = launcherPath,
                WorkingDirectory = Paths.GameRootPath,
                Arguments = arguments,
                UseShellExecute = false
            };

            Process.Start(startInfo);
            Logger.LogInfo($"Ctrl+P requested {_settings.PlayerCount}-player split-screen on monitor #{request.TargetMonitor + 1}.");
            return null;
        }
        catch (Exception exception)
        {
            Logger.LogError(exception);
            return $"Impossible d’activer le split-screen : {exception.Message}";
        }
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
}