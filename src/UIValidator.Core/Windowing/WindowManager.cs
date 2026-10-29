using UIValidator.Core.Config;
using UIValidator.Core.Model;
using static UIValidator.Core.Native.NativeMethods;

namespace UIValidator.Core.Windowing;

/// <summary>Geometry queries and resizing for top-level windows.</summary>
public static class WindowManager
{
    /// <summary>Makes the current process Per-Monitor V2 DPI aware (no-op if already set by the manifest).</summary>
    public static void EnsurePerMonitorDpiAwareness() => SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

    /// <summary>Raw window rectangle, including invisible resize borders.</summary>
    public static Rect GetWindowRect(IntPtr hwnd) =>
        NativeMethods_GetWindowRect(hwnd);

    /// <summary>Visible window bounds (DWM extended frame, without invisible borders).</summary>
    public static Rect GetVisibleBounds(IntPtr hwnd)
    {
        if (DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out var r, System.Runtime.InteropServices.Marshal.SizeOf<RECT>()) == 0)
            return Rect.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
        return GetWindowRect(hwnd);
    }

    /// <summary>Client area in screen coordinates.</summary>
    public static Rect GetClientBounds(IntPtr hwnd)
    {
        GetClientRect(hwnd, out var c);
        var origin = new POINT();
        ClientToScreen(hwnd, ref origin);
        return new Rect(origin.X, origin.Y, c.Right - c.Left, c.Bottom - c.Top);
    }

    /// <summary>Work area (screen minus taskbar) of the monitor hosting the window.</summary>
    public static Rect GetWorkArea(IntPtr hwnd) => GetMonitorRects(MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST)).Work;

    public static (Rect Monitor, Rect Work, string Device) GetPrimaryMonitor() =>
        GetMonitorRects(MonitorFromPoint(new POINT(), MONITOR_DEFAULTTOPRIMARY));

    /// <summary>Scale factor of the window (1.0 = 96 DPI).</summary>
    public static double GetScale(IntPtr hwnd)
    {
        var dpi = GetDpiForWindow(hwnd);
        if (dpi == 0)
        {
            GetDpiForMonitor(MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST), 0, out dpi, out _);
        }
        return dpi == 0 ? 1.0 : dpi / 96.0;
    }

    /// <summary>Scale of the monitor hosting the window, as a percentage (100, 125, ...).</summary>
    public static int GetMonitorScalePercent(IntPtr hwnd)
    {
        GetDpiForMonitor(MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST), 0, out var dpi, out _);
        return dpi == 0 ? 100 : (int)Math.Round(dpi * 100 / 96.0);
    }

    /// <summary>
    /// Applies a window size: "maximized", "current" or "WIDTHxHEIGHT" (visible size in physical pixels).
    /// The window is always moved to the primary monitor (the one whose resolution and DPI the
    /// validator controls) and centered in its work area.
    /// </summary>
    public static void ApplySize(IntPtr hwnd, string size)
    {
        if (size == "current") return;

        if (IsZoomed(hwnd)) ShowWindow(hwnd, SW_RESTORE);
        var work = GetPrimaryMonitor().Work;

        if (size == "maximized")
        {
            // Move onto the primary monitor first so that maximizing happens there.
            var r = GetWindowRect(hwnd);
            SetWindowPos(hwnd, IntPtr.Zero, work.X + 20, work.Y + 20, Math.Min(r.Width, work.Width), Math.Min(r.Height, work.Height), SWP_NOZORDER);
            ShowWindow(hwnd, SW_MAXIMIZE);
            return;
        }
        if (!Size2D.TryParse(size, out var target))
            throw new ArgumentException($"Invalid window size '{size}'.");

        // Move first: invisible border sizes depend on the monitor DPI.
        SetWindowPos(hwnd, IntPtr.Zero, work.X, work.Y, target.Width, target.Height, SWP_NOZORDER);

        // Compensate for invisible DWM borders so the *visible* size matches the request.
        var raw = GetWindowRect(hwnd);
        var visible = GetVisibleBounds(hwnd);
        var extraW = raw.Width - visible.Width;
        var extraH = raw.Height - visible.Height;
        var offsetX = visible.X - raw.X;

        var x = work.X + Math.Max(0, (work.Width - target.Width) / 2) - offsetX;
        var y = work.Y + Math.Max(0, (work.Height - target.Height) / 2);
        SetWindowPos(hwnd, IntPtr.Zero, x, y, target.Width + extraW, target.Height + extraH, SWP_NOZORDER);
    }

    /// <summary>Resolution of the monitor hosting the window, e.g. "1920x1080".</summary>
    public static string GetMonitorResolution(IntPtr hwnd)
    {
        var m = GetMonitorRects(MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST)).Monitor;
        return $"{m.Width}x{m.Height}";
    }

    public static void BringToFront(IntPtr hwnd) => SetForegroundWindow(hwnd);

    public static bool Exists(IntPtr hwnd) => IsWindow(hwnd);

    private static Rect NativeMethods_GetWindowRect(IntPtr hwnd)
    {
        Native.NativeMethods.GetWindowRect(hwnd, out var r);
        return Rect.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
    }

    private static (Rect Monitor, Rect Work, string Device) GetMonitorRects(IntPtr monitor)
    {
        var info = new MONITORINFOEX { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFOEX>() };
        GetMonitorInfo(monitor, ref info);
        return (Rect.FromLTRB(info.rcMonitor.Left, info.rcMonitor.Top, info.rcMonitor.Right, info.rcMonitor.Bottom),
                Rect.FromLTRB(info.rcWork.Left, info.rcWork.Top, info.rcWork.Right, info.rcWork.Bottom),
                info.szDevice);
    }
}
