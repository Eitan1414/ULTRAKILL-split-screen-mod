using System.Collections;
using System.Reflection;

namespace ULTRAKILLSplitScreen.Plugin;

internal static class GamepadIsolation
{
    public static bool TryApply(int assignedOrdinal, string profile, out string message)
    {
        message = string.Empty;
        if (!TryGetInputSystem(out Type? inputSystemType, out Type? gamepadType, out List<object> allGamepads, out message))
            return false;

        if (assignedOrdinal < 0)
        {
            message = $"Gamepad isolation disabled for this instance; detected {Describe(allGamepads)}.";
            return true;
        }

        string normalizedProfile = NormalizeProfile(profile);
        List<object> matchingGamepads = allGamepads
            .Where(gamepad => MatchesProfile(GetDeviceName(gamepad), normalizedProfile))
            .ToList();

        if (matchingGamepads.Count == 0)
        {
            message = $"No {normalizedProfile} gamepad matched. Detected: {Describe(allGamepads)}.";
            return false;
        }

        if (assignedOrdinal >= matchingGamepads.Count)
        {
            message = $"Requested {normalizedProfile} gamepad ordinal #{assignedOrdinal}, but only {matchingGamepads.Count} matched: {Describe(matchingGamepads)}.";
            return false;
        }

        MethodInfo? disableMethod = FindDeviceMethod(inputSystemType!, "DisableDevice");
        MethodInfo? enableMethod = FindDeviceMethod(inputSystemType!, "EnableDevice");
        if (disableMethod is null)
        {
            message = "Unity Input System DisableDevice method was not found.";
            return false;
        }

        object selected = matchingGamepads[assignedOrdinal];
        if (enableMethod is not null)
            InvokeDeviceMethod(enableMethod, selected);

        foreach (object gamepad in allGamepads)
        {
            if (!ReferenceEquals(gamepad, selected))
                InvokeDeviceMethod(disableMethod, gamepad);
        }

        message = $"Assigned {normalizedProfile} gamepad ordinal #{assignedOrdinal} ({GetDeviceName(selected)}); disabled {allGamepads.Count - 1} other gamepad(s).";
        return true;
    }

    public static string DescribeAvailable()
    {
        return TryGetInputSystem(out _, out _, out List<object> gamepads, out string message)
            ? Describe(gamepads)
            : message;
    }

    private static bool TryGetInputSystem(
        out Type? inputSystemType,
        out Type? gamepadType,
        out List<object> gamepads,
        out string message)
    {
        inputSystemType = null;
        gamepadType = null;
        gamepads = [];
        message = string.Empty;

        Assembly? inputAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, "Unity.InputSystem", StringComparison.Ordinal));
        if (inputAssembly is null)
        {
            message = "Unity.InputSystem is not loaded yet.";
            return false;
        }

        inputSystemType = inputAssembly.GetType("UnityEngine.InputSystem.InputSystem", false);
        gamepadType = inputAssembly.GetType("UnityEngine.InputSystem.Gamepad", false);
        if (inputSystemType is null || gamepadType is null)
        {
            message = "Unity Input System gamepad types were not found.";
            return false;
        }

        PropertyInfo? devicesProperty = inputSystemType.GetProperty("devices", BindingFlags.Public | BindingFlags.Static);
        object? devicesValue = devicesProperty?.GetValue(null);
        if (devicesValue is not IEnumerable devices)
        {
            message = "Unity Input System devices are not available yet.";
            return false;
        }

        foreach (object? device in devices)
        {
            if (device is not null && gamepadType.IsInstanceOfType(device))
                gamepads.Add(device);
        }

        if (gamepads.Count == 0)
        {
            message = "No compatible gamepad was detected.";
            return false;
        }

        return true;
    }

    private static bool MatchesProfile(string deviceName, string profile)
    {
        if (profile == "auto")
            return true;

        string name = deviceName.ToLowerInvariant();
        return profile switch
        {
            "xbox" => name.Contains("xbox", StringComparison.Ordinal)
                || name.Contains("xinput", StringComparison.Ordinal)
                || name.Contains("x-box", StringComparison.Ordinal),
            "playstation" => name.Contains("dualshock", StringComparison.Ordinal)
                || name.Contains("dualsense", StringComparison.Ordinal)
                || name.Contains("playstation", StringComparison.Ordinal)
                || name.Contains("wireless controller", StringComparison.Ordinal)
                || name.Contains("sony", StringComparison.Ordinal),
            "switch" => name.Contains("switch", StringComparison.Ordinal)
                || name.Contains("nintendo", StringComparison.Ordinal)
                || name.Contains("pro controller", StringComparison.Ordinal)
                || name.Contains("joy-con", StringComparison.Ordinal),
            _ => true
        };
    }

    private static string NormalizeProfile(string? profile)
    {
        return profile?.Trim().ToLowerInvariant() switch
        {
            "xbox" => "xbox",
            "playstation" => "playstation",
            "ps4" => "playstation",
            "ps5" => "playstation",
            "switch" => "switch",
            "nintendo" => "switch",
            _ => "auto"
        };
    }

    private static MethodInfo? FindDeviceMethod(Type inputSystemType, string name)
    {
        return inputSystemType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => string.Equals(method.Name, name, StringComparison.Ordinal))
            .Where(method => method.GetParameters().Length is 1 or 2)
            .OrderBy(method => method.GetParameters().Length)
            .FirstOrDefault();
    }

    private static void InvokeDeviceMethod(MethodInfo method, object device)
    {
        ParameterInfo[] parameters = method.GetParameters();
        object?[] arguments = parameters.Length == 1
            ? [device]
            : [device, false];
        method.Invoke(null, arguments);
    }

    private static string Describe(IReadOnlyList<object> devices)
    {
        return string.Join(", ", devices.Select((device, index) => $"#{index} {GetDeviceName(device)}"));
    }

    private static string GetDeviceName(object device)
    {
        PropertyInfo? displayName = device.GetType().GetProperty("displayName", BindingFlags.Public | BindingFlags.Instance);
        string? name = displayName?.GetValue(device)?.ToString();
        return string.IsNullOrWhiteSpace(name) ? device.GetType().Name : name;
    }
}