using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ULTRAKILLSplitScreen.Launcher;

internal static class NativeWindow
{
    private const int GwlStyle = -16;
    private const long WsCaption = 0x00C00000L;
    private const long WsThickFrame = 0x00040000L;
    private const long WsMinimizeBox = 0x00020000L;
    private const long WsMaximizeBox = 0x00010000L;
    private const long WsSysMenu = 0x00080000L;

    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;
    private const uint SwpShowWindow = 0x0040;
    private const uint MonitorInfoPrimary = 0x00000001;

    public static int ScreenWidth => GetPrimaryMonitor().Bounds.Width;
    public static int ScreenHeight => GetPrimaryMonitor().Bounds.Height;

    public static IReadOnlyList<MonitorDisplay> GetMonitors()
    {
        var monitors = new List<MonitorDisplay>();
        MonitorEnumProc callback = (monitorHandle, _, ref NativeRect _, _) =>
        {
            var info = new MonitorInfoEx
            {
                Size = Marshal.SizeOf<MonitorInfoEx>(),
                DeviceName = string.Empty
            };

            if (!GetMonitorInfo(monitorHandle, ref info))
                return true;

            WindowArea bounds = ToArea(info.Monitor);
            WindowArea workArea = ToArea(info.Work);
            bool primary = (info.Flags & MonitorInfoPrimary) != 0;
            monitors.Add(new MonitorDisplay(0, bounds, workArea, primary, info.DeviceName ?? string.Empty));
            return true;
        };

        _ = EnumDisplayMonitors(nint.Zero, nint.Zero, callback, nint.Zero);

        if (monitors.Count == 0)
        {
            WindowArea fallback = new(0, 0, GetSystemMetrics(0), GetSystemMetrics(1));
            return [new MonitorDisplay(0, fallback, fallback, true, "Primary")];
        }

        MonitorDisplay[] ordered = monitors
            .OrderByDescending(monitor => monitor.IsPrimary)
            .ThenBy(monitor => monitor.Bounds.X)
            .ThenBy(monitor => monitor.Bounds.Y)
            .Select((monitor, index) => monitor with { Index = index })
            .ToArray();
        return ordered;
    }

    public static MonitorDisplay GetMonitor(int requestedIndex, out bool fellBack)
    {
        IReadOnlyList<MonitorDisplay> monitors = GetMonitors();
        if (requestedIndex >= 0 && requestedIndex < monitors.Count)
        {
            fellBack = false;
            return monitors[requestedIndex];
        }

        fellBack = requestedIndex != 0;
        return monitors[0];
    }

    public static async Task<nint> WaitForMainWindowAsync(Process process, int timeoutMs)
    {
        long deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            if (process.HasExited)
                throw new InvalidOperationException($"ULTRAKILL process {process.Id} exited before creating a window.");

            process.Refresh();
            nint handle = process.MainWindowHandle;
            if (handle != nint.Zero)
                return handle;

            await Task.Delay(250).ConfigureAwait(false);
        }

        throw new TimeoutException($"ULTRAKILL process {process.Id} did not create a window within {timeoutMs} ms.");
    }

    public static void ApplyLayout(nint window, WindowArea area, bool borderless)
    {
        if (window == nint.Zero)
            throw new ArgumentException("Window handle cannot be zero.", nameof(window));

        if (borderless)
        {
            long style = GetWindowStyle(window).ToInt64();
            style &= ~(WsCaption | WsThickFrame | WsMinimizeBox | WsMaximizeBox | WsSysMenu);
            SetWindowStyle(window, new nint(style));
        }

        bool success = SetWindowPos(
            window,
            nint.Zero,
            area.X,
            area.Y,
            area.Width,
            area.Height,
            SwpNoZOrder | SwpNoActivate | SwpFrameChanged | SwpShowWindow);

        if (!success)
            throw new InvalidOperationException($"SetWindowPos failed with Win32 error {Marshal.GetLastWin32Error()}.");
    }

    private static MonitorDisplay GetPrimaryMonitor()
    {
        return GetMonitors().FirstOrDefault(monitor => monitor.IsPrimary) is { } primary
            ? primary
            : GetMonitors()[0];
    }

    private static WindowArea ToArea(NativeRect rect)
    {
        return new WindowArea(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

    private static nint GetWindowStyle(nint window)
    {
        return nint.Size == 8
            ? GetWindowLongPtr64(window, GwlStyle)
            : new nint(GetWindowLong32(window, GwlStyle));
    }

    private static void SetWindowStyle(nint window, nint style)
    {
        if (nint.Size == 8)
            _ = SetWindowLongPtr64(window, GwlStyle, style);
        else
            _ = SetWindowLong32(window, GwlStyle, style.ToInt32());
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfoEx
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
    }

    private delegate bool MonitorEnumProc(nint monitor, nint deviceContext, ref NativeRect rect, nint data);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(
        nint deviceContext,
        nint clipRect,
        MonitorEnumProc callback,
        nint data);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfoEx info);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        nint window,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
    private static extern int GetWindowLong32(nint window, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern nint GetWindowLongPtr64(nint window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
    private static extern int SetWindowLong32(nint window, int index, int value);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern nint SetWindowLongPtr64(nint window, int index, nint value);
}

internal readonly record struct MonitorDisplay(
    int Index,
    WindowArea Bounds,
    WindowArea WorkArea,
    bool IsPrimary,
    string DeviceName);