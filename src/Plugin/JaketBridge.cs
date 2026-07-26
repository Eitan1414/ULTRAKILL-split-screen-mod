using System.Collections;
using System.Reflection;
using UnityEngine;

namespace ULTRAKILLSplitScreen.Plugin;

internal static class JaketBridge
{
    private const string LobbyControllerName = "Jaket.Net.LobbyController";

    public static IEnumerator Run(
        InstanceSettings settings,
        Action<string> logInfo,
        Action<string> logWarning)
    {
        yield return new WaitForSecondsRealtime(settings.JaketStartDelaySeconds);

        float deadline = Time.realtimeSinceStartup + settings.JaketTimeoutSeconds;
        Type? lobbyController = null;
        while (Time.realtimeSinceStartup < deadline)
        {
            lobbyController = FindType(LobbyControllerName);
            if (lobbyController is not null && IsJaketInitialized())
                break;
            yield return new WaitForSecondsRealtime(1f);
        }

        if (lobbyController is null)
        {
            logWarning("Jaket was not detected. Install Jaket.dll in BepInEx/plugins to use automatic co-op.");
            yield break;
        }

        if (!IsJaketInitialized())
        {
            logWarning("Jaket was detected but did not finish initializing before the timeout.");
            yield break;
        }

        IEnumerator routine = settings.JaketHost
            ? Host(lobbyController, settings, logInfo, logWarning)
            : Join(lobbyController, settings, logInfo, logWarning);
        yield return routine;
    }

    private static IEnumerator Host(
        Type lobbyController,
        InstanceSettings settings,
        Action<string> logInfo,
        Action<string> logWarning)
    {
        MethodInfo? createLobby = lobbyController.GetMethod("CreateLobby", BindingFlags.Public | BindingFlags.Static);
        FieldInfo? lobbyField = lobbyController.GetField("Lobby", BindingFlags.Public | BindingFlags.Static);
        if (createLobby is null || lobbyField is null)
        {
            logWarning("The installed Jaket version does not expose the expected CreateLobby/Lobby API.");
            yield break;
        }

        try
        {
            createLobby.Invoke(null, null);
            logInfo("Requested a private Jaket lobby for the host instance.");
        }
        catch (Exception exception)
        {
            logWarning($"Jaket CreateLobby failed: {Unwrap(exception).Message}");
            yield break;
        }

        float deadline = Time.realtimeSinceStartup + settings.JaketTimeoutSeconds;
        while (Time.realtimeSinceStartup < deadline)
        {
            object? lobby = lobbyField.GetValue(null);
            if (lobby is not null && TryGetLobbyCode(lobby, out string code))
            {
                try
                {
                    string? directory = Path.GetDirectoryName(settings.JaketCodeFile);
                    if (!string.IsNullOrWhiteSpace(directory))
                        Directory.CreateDirectory(directory);
                    File.WriteAllText(settings.JaketCodeFile, code);
                    logInfo($"Jaket lobby {code} created; code shared with local client instances.");
                }
                catch (Exception exception)
                {
                    logWarning($"Could not write the Jaket lobby code file: {exception.Message}");
                }
                yield break;
            }

            yield return new WaitForSecondsRealtime(0.5f);
        }

        logWarning("Jaket did not create a lobby before the timeout.");
    }

    private static IEnumerator Join(
        Type lobbyController,
        InstanceSettings settings,
        Action<string> logInfo,
        Action<string> logWarning)
    {
        if (string.IsNullOrWhiteSpace(settings.JaketCodeFile))
        {
            logWarning("No Jaket lobby code file was configured.");
            yield break;
        }

        ulong code = 0;
        float deadline = Time.realtimeSinceStartup + settings.JaketTimeoutSeconds;
        while (Time.realtimeSinceStartup < deadline)
        {
            try
            {
                if (File.Exists(settings.JaketCodeFile)
                    && ulong.TryParse(File.ReadAllText(settings.JaketCodeFile).Trim(), out code)
                    && code != 0)
                {
                    break;
                }
            }
            catch (IOException)
            {
                // The host may still be replacing the file. Retry on the next tick.
            }

            yield return new WaitForSecondsRealtime(0.5f);
        }

        if (code == 0)
        {
            logWarning("No Jaket lobby code appeared before the timeout.");
            yield break;
        }

        MethodInfo? joinLobby = lobbyController.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(method => method.Name == "JoinLobby" && method.GetParameters().Length == 1);
        if (joinLobby is null)
        {
            logWarning("The installed Jaket version does not expose the expected JoinLobby API.");
            yield break;
        }

        Type lobbyType = joinLobby.GetParameters()[0].ParameterType;
        object? lobbyValue;
        try
        {
            lobbyValue = Activator.CreateInstance(lobbyType, [code]);
        }
        catch (Exception exception)
        {
            logWarning($"Could not create Jaket's lobby value: {Unwrap(exception).Message}");
            yield break;
        }

        if (lobbyValue is null)
        {
            logWarning("Could not construct a Jaket lobby value from the shared code.");
            yield break;
        }

        try
        {
            joinLobby.Invoke(null, [lobbyValue]);
            logInfo($"Requested joining Jaket lobby {code}.");
        }
        catch (Exception exception)
        {
            logWarning($"Jaket JoinLobby failed: {Unwrap(exception).Message}");
            yield break;
        }

        PropertyInfo? onlineProperty = lobbyController.GetProperty("Online", BindingFlags.Public | BindingFlags.Static);
        deadline = Time.realtimeSinceStartup + settings.JaketTimeoutSeconds;
        while (Time.realtimeSinceStartup < deadline)
        {
            if (onlineProperty?.GetValue(null) is bool online && online)
            {
                logInfo($"Player {settings.PlayerIndex} joined the Jaket lobby.");
                yield break;
            }
            yield return new WaitForSecondsRealtime(0.5f);
        }

        logWarning("Jaket did not report a successful lobby join before the timeout. Multiple local instances normally need distinct Steam identities.");
    }

    private static bool IsJaketInitialized()
    {
        Type? pluginType = FindType("Jaket.Plugin");
        FieldInfo? instanceField = pluginType?.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
        object? instance = instanceField?.GetValue(null);
        if (instance is null)
            return false;

        FieldInfo? initializedField = pluginType?.GetField("Initialized", BindingFlags.Public | BindingFlags.Instance);
        return initializedField?.GetValue(instance) is bool initialized && initialized;
    }

    private static Type? FindType(string fullName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type? type = assembly.GetType(fullName, false);
            if (type is not null)
                return type;
        }
        return null;
    }

    private static bool TryGetLobbyCode(object lobby, out string code)
    {
        code = string.Empty;
        PropertyInfo? idProperty = lobby.GetType().GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
        object? id = idProperty?.GetValue(lobby);
        string? text = id?.ToString();
        if (ulong.TryParse(text, out ulong parsed) && parsed != 0)
        {
            code = parsed.ToString();
            return true;
        }
        return false;
    }

    private static Exception Unwrap(Exception exception)
    {
        return exception is TargetInvocationException { InnerException: not null } invocation
            ? invocation.InnerException
            : exception;
    }
}
