using System.Collections;
using BepInEx;
using UnityEngine;

namespace ULTRAKILLSplitScreen.Plugin;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInProcess("ULTRAKILL.exe")]
public sealed class SplitScreenPlugin : BaseUnityPlugin
{
    private const string PluginGuid = "eitan1414.ultrakill.splitscreen";
    private const string PluginName = "ULTRAKILL Split-Screen Bridge";
    private const string PluginVersion = "0.1.0";

    private InstanceSettings _settings = null!;

    private void Awake()
    {
        _settings = InstanceSettings.FromEnvironment();
        Application.runInBackground = true;

        if (_settings.Muted)
            AudioListener.volume = 0f;

        Logger.LogInfo($"Loaded split-screen bridge for player {_settings.PlayerIndex} ({_settings.Width}x{_settings.Height}).");
        StartCoroutine(ReapplyWindowSettings());
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            Application.runInBackground = true;
    }

    private IEnumerator ReapplyWindowSettings()
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            Application.runInBackground = true;
            Screen.fullScreenMode = FullScreenMode.Windowed;
            Screen.SetResolution(_settings.Width, _settings.Height, false);
            yield return new WaitForSecondsRealtime(0.5f);
        }
    }
}
