using System.Diagnostics;
using System.Runtime.InteropServices;

/// <summary>
/// Moves windows that dev runs open (nwserver) onto the monitor named by "devDisplay" in config/local.json,
/// without activating them, so test runs leave the main monitor free.
/// </summary>
static class DevDisplay
{
    /// <summary>Watches the process for its window and moves it there as soon as it appears.</summary>
    public static void Follow(Process p, string? display)
    {
        if (string.IsNullOrEmpty(display) || !OperatingSystem.IsWindows() || WorkArea(display) is not { } area) return;
        new Thread(() =>
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed.TotalSeconds < 20)
            {
                try
                {
                    if (p.HasExited) return;
                    p.Refresh();
                    if (p.MainWindowHandle != IntPtr.Zero)
                    {
                        SetWindowPos(p.MainWindowHandle, IntPtr.Zero, area.Left, area.Top, 0, 0, NoSize | NoZOrder | NoActivate);
                        return;
                    }
                }
                catch (InvalidOperationException) { return; }
                Thread.Sleep(15);
            }
        })
        { IsBackground = true, Name = "dev-display" }.Start();
    }

    /// <summary>
    /// The work area of the monitor whose device name ends with <paramref name="display"/>, or of another
    /// secondary monitor when that one is switched off; null when only the primary is left.
    /// </summary>
    static Rect? WorkArea(string display)
    {
        Rect? named = null, secondary = null;
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, _, _, _) =>
        {
            var info = new MonitorInfoEx { Size = Marshal.SizeOf<MonitorInfoEx>() };
            if (!GetMonitorInfo(monitor, ref info)) return true;
            if (info.Device.EndsWith(display, StringComparison.OrdinalIgnoreCase)) named = info.Work;
            else if ((info.Flags & PrimaryMonitor) == 0) secondary ??= info.Work;
            return true;
        }, IntPtr.Zero);
        return named ?? secondary;
    }

    const uint PrimaryMonitor = 0x1, NoSize = 0x1, NoZOrder = 0x4, NoActivate = 0x10;

    [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct MonitorInfoEx
    {
        public int Size;
        public Rect Monitor, Work;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device;
    }

    delegate bool MonitorEnum(IntPtr monitor, IntPtr dc, IntPtr rect, IntPtr data);

    [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorEnum callback, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfoEx info);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int cx, int cy, uint flags);
}
