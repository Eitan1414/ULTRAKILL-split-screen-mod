using System.Collections;
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
    private const string PluginVersion = "0.2.0";

    private InstanceSettings _settings = null!;

    private void Awake()
    {
        _settings = InstanceSettings.FromEnvironment();
        Application.runInBackground = true;

        if (_settings.Muted)
            AudioListener.volume = 0f;

        Logger.LogInfo($"Loaded split-screen bridge for player {_settings.PlayerIndex}/{_settings.PlayerCount} ({_settings.Width}x{_settings.Height}).");
        StartCoroutine(ReapplyWindowSettings());

        if (_settings.InputIsolation)
            StartCoroutine(MaintainGamepadIsolation());

        if (_settings.JaketEnabled)
        {
            StartCoroutine(JaketBridge.Run(
                _settings,
                message => Logger.LogInfo(message),
                message => Logger.LogWarning(message)));
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            Application.runInBackground = true;
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

    private IEnumerator MaintainGamepadIsolation()
    {
        string previousMessage = string.Empty;
        while (true)
        {
            bool success = false;
            string message;
            try
            {
                success = GamepadIsolation.TryApply(_settings.GamepadIndex, out message);
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
