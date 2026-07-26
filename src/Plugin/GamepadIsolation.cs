using System.Collections;
using System.Reflection;

namespace ULTRAKILLSplitScreen.Plugin;

internal static class GamepadIsolation
{
    public static bool TryApply(int assignedIndex, out string message)
    {
        message = string.Empty;
        Assembly? inputAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, "Unity.InputSystem", StringComparison.Ordinal));
        if (inputAssembly is null)
        {
            message = "Unity.InputSystem is not loaded yet.";
            return false;
        }

        Type? inputSystemType = inputAssembly.GetType("UnityEngine.InputSystem.InputSystem", false);
        Type? gamepadType = inputAssembly.GetType("UnityEngine.InputSystem.Gamepad", false);
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

        var gamepads = new List<object>();
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

        if (assignedIndex < 0)
        {
            message = $"Gamepad isolation disabled for this instance; detected {Describe(gamepads)}.";
            return true;
        }

        if (assignedIndex >= gamepads.Count)
        {
            message = $"Requested gamepad #{assignedIndex}, but only {gamepads.Count} gamepad(s) were detected: {Describe(gamepads)}.";
            return false;
        }

        MethodInfo? disableMethod = FindDeviceMethod(inputSystemType, "DisableDevice");
        MethodInfo? enableMethod = FindDeviceMethod(inputSystemType, "EnableDevice");
        if (disableMethod is null)
        {
            message = "Unity Input System DisableDevice method was not found.";
            return false;
        }

        object selected = gamepads[assignedIndex];
        if (enableMethod is not null)
            InvokeDeviceMethod(enableMethod, selected);

        for (int index = 0; index < gamepads.Count; index++)
        {
            if (index != assignedIndex)
                InvokeDeviceMethod(disableMethod, gamepads[index]);
        }

        message = $"Assigned gamepad #{assignedIndex} ({GetDeviceName(selected)}); disabled {gamepads.Count - 1} other gamepad(s).";
        return true;
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
