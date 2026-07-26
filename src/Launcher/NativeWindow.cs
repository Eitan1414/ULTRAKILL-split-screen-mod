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

    public static int ScreenWidth => GetSystemMetrics(0);
    public static int ScreenHeight => GetSystemMetrics(1);

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

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

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
