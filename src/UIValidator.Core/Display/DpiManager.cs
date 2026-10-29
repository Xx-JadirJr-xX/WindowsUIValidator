using System.Runtime.InteropServices;
using UIValidator.Core.Windowing;
using static UIValidator.Core.Native.NativeMethods;

namespace UIValidator.Core.Display;

/// <summary>Current display scale information for the primary monitor (percentages).</summary>
public readonly record struct ScaleInfo(int Current, int Recommended, int Minimum, int Maximum);

/// <summary>
/// Changes the display scale ("Scale and layout" in Settings) of the primary monitor.
/// <para>
/// Windows has no public API for this. The implementation uses the undocumented
/// <c>DisplayConfigGetDeviceInfo</c>/<c>DisplayConfigSetDeviceInfo</c> packet types -3/-4,
/// which the Settings app itself uses. Scales are expressed as steps relative to the
/// recommended value. It has been stable since Windows 10 1607, but may break in future
/// versions; for fully reproducible CI use a VM per scale instead.
/// </para>
/// </summary>
public static class DpiManager
{
    /// <summary>Scale steps offered by Windows, in order.</summary>
    public static readonly int[] Steps = [100, 125, 150, 175, 200, 225, 250, 300, 350, 400, 450, 500];

    public static ScaleInfo GetScale()
    {
        var (adapter, sourceId) = FindPrimarySource();
        var get = new DISPLAYCONFIG_SOURCE_DPI_SCALE_GET
        {
            header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
            {
                type = DISPLAYCONFIG_DEVICE_INFO_GET_DPI_SCALE,
                size = Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DPI_SCALE_GET>(),
                adapterId = adapter,
                id = sourceId,
            },
        };
        var rc = DisplayConfigGetDeviceInfo(ref get);
        if (rc != 0) throw new InvalidOperationException($"DisplayConfigGetDeviceInfo(DPI) failed with code {rc}.");

        // minScaleRel is the (negative) distance from the recommended step to 100%.
        var recommendedIdx = Math.Abs(get.minScaleRel);
        int Pct(int idx) => Steps[Math.Clamp(idx, 0, Steps.Length - 1)];
        return new ScaleInfo(
            Pct(recommendedIdx + get.curScaleRel),
            Pct(recommendedIdx),
            Pct(0),
            Pct(recommendedIdx + get.maxScaleRel));
    }

    /// <summary>Scale percentages the primary monitor accepts at its current resolution.</summary>
    public static List<int> GetSupportedScales()
    {
        var info = GetScale();
        return Steps.Where(s => s >= info.Minimum && s <= info.Maximum).ToList();
    }

    public static void SetScale(int percent)
    {
        var targetIdx = Array.IndexOf(Steps, percent);
        if (targetIdx < 0)
            throw new ArgumentException($"Scale {percent}% is not a valid Windows scale step ({string.Join(", ", Steps)}).");

        var info = GetScale();
        if (info.Current == percent) return;
        if (percent > info.Maximum)
            throw new NotSupportedException($"Scale {percent}% exceeds the maximum ({info.Maximum}%) for the current resolution.");

        var (adapter, sourceId) = FindPrimarySource();
        var set = new DISPLAYCONFIG_SOURCE_DPI_SCALE_SET
        {
            header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
            {
                type = DISPLAYCONFIG_DEVICE_INFO_SET_DPI_SCALE,
                size = Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DPI_SCALE_SET>(),
                adapterId = adapter,
                id = sourceId,
            },
            scaleRel = targetIdx - Array.IndexOf(Steps, info.Recommended),
        };
        var rc = DisplayConfigSetDeviceInfo(ref set);
        if (rc != 0) throw new InvalidOperationException($"DisplayConfigSetDeviceInfo(DPI) failed with code {rc}.");

        // Wait until the monitor reports the new DPI (the change is asynchronous).
        var deadline = Environment.TickCount64 + 5000;
        while (Environment.TickCount64 < deadline && CurrentMonitorPercent() != percent)
            Thread.Sleep(200);
        Thread.Sleep(1000);
    }

    private static int CurrentMonitorPercent()
    {
        var monitor = MonitorFromPoint(new POINT(), MONITOR_DEFAULTTOPRIMARY);
        GetDpiForMonitor(monitor, 0, out var dpi, out _);
        return (int)Math.Round(dpi * 100 / 96.0);
    }

    /// <summary>Finds the DisplayConfig source matching the primary monitor's GDI device name.</summary>
    private static (LUID Adapter, uint SourceId) FindPrimarySource()
    {
        var device = WindowManager.GetPrimaryMonitor().Device;
        if (GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out var numPaths, out var numModes) != 0)
            throw new InvalidOperationException("GetDisplayConfigBufferSizes failed.");

        var paths = new DISPLAYCONFIG_PATH_INFO[numPaths];
        var modes = new DISPLAYCONFIG_MODE_INFO[numModes];
        if (QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref numPaths, paths, ref numModes, modes, IntPtr.Zero) != 0)
            throw new InvalidOperationException("QueryDisplayConfig failed.");

        foreach (var path in paths.Take((int)numPaths))
        {
            var name = new DISPLAYCONFIG_SOURCE_DEVICE_NAME
            {
                header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                {
                    type = DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME,
                    size = Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DEVICE_NAME>(),
                    adapterId = path.sourceInfo.adapterId,
                    id = path.sourceInfo.id,
                },
            };
            if (DisplayConfigGetDeviceInfo(ref name) == 0 && string.Equals(name.viewGdiDeviceName, device, StringComparison.OrdinalIgnoreCase))
                return (path.sourceInfo.adapterId, path.sourceInfo.id);
        }

        if (numPaths > 0) return (paths[0].sourceInfo.adapterId, paths[0].sourceInfo.id);
        throw new InvalidOperationException("No active display path found.");
    }
}
