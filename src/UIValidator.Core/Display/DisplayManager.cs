using System.Runtime.InteropServices;
using System.Text.Json;
using UIValidator.Core.Config;
using UIValidator.Core.Windowing;
using static UIValidator.Core.Native.NativeMethods;

namespace UIValidator.Core.Display;

/// <summary>A display mode supported by the primary monitor.</summary>
public sealed record DisplayMode(int Width, int Height, int Frequency, int BitsPerPixel)
{
    public override string ToString() => $"{Width}x{Height} @ {Frequency}Hz";
}

/// <summary>Display state saved before the validator changes anything.</summary>
public sealed class DisplayState
{
    public string Device { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public int Frequency { get; set; }
    public int? DpiScale { get; set; }
    public DateTimeOffset SavedAt { get; set; } = DateTimeOffset.Now;

    public override string ToString() => $"{Width}x{Height} @ {Frequency}Hz, scale {(DpiScale is { } d ? d + "%" : "unknown")}";
}

/// <summary>
/// Changes the resolution of the primary monitor.
/// Changes are applied dynamically (not written to the registry), so a reboot or log-off
/// always returns to the user's settings. The original state is also persisted to
/// <see cref="StateFile"/> so it can be restored after a crash with <c>uivalidator restore-display</c>.
/// </summary>
public static class DisplayManager
{
    public static string StateFile { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UIValidator", "display-state.json");

    public static string PrimaryDevice => WindowManager.GetPrimaryMonitor().Device;

    public static DisplayMode GetCurrentMode()
    {
        var dm = NewDevMode();
        if (!EnumDisplaySettings(PrimaryDevice, ENUM_CURRENT_SETTINGS, ref dm))
            throw new InvalidOperationException("Unable to read the current display mode.");
        return new DisplayMode(dm.dmPelsWidth, dm.dmPelsHeight, dm.dmDisplayFrequency, dm.dmBitsPerPel);
    }

    public static List<DisplayMode> GetSupportedModes()
    {
        var modes = new HashSet<DisplayMode>();
        var dm = NewDevMode();
        for (var i = 0; EnumDisplaySettings(PrimaryDevice, i, ref dm); i++)
            if (dm.dmBitsPerPel >= 24)
                modes.Add(new DisplayMode(dm.dmPelsWidth, dm.dmPelsHeight, dm.dmDisplayFrequency, dm.dmBitsPerPel));
        return modes.OrderByDescending(m => m.Width).ThenByDescending(m => m.Height).ThenByDescending(m => m.Frequency).ToList();
    }

    public static bool IsSupported(Size2D size) => GetSupportedModes().Any(m => m.Width == size.Width && m.Height == size.Height);

    /// <summary>Applies a resolution using the highest refresh rate available for it.</summary>
    public static void SetResolution(Size2D size)
    {
        var current = GetCurrentMode();
        if (current.Width == size.Width && current.Height == size.Height) return;

        var mode = GetSupportedModes()
            .Where(m => m.Width == size.Width && m.Height == size.Height)
            .OrderByDescending(m => m.Frequency == current.Frequency).ThenByDescending(m => m.Frequency)
            .FirstOrDefault()
            ?? throw new NotSupportedException($"Resolution {size} is not supported by the primary display.");

        Apply(mode.Width, mode.Height, mode.Frequency);
    }

    /// <summary>Saves the current state to disk unless a previous unrestored state exists.</summary>
    public static DisplayState SaveState(int? dpiScale)
    {
        var existing = LoadState();
        if (existing is not null) return existing; // Never overwrite the user's true original settings.

        var mode = GetCurrentMode();
        var state = new DisplayState { Device = PrimaryDevice, Width = mode.Width, Height = mode.Height, Frequency = mode.Frequency, DpiScale = dpiScale };
        Directory.CreateDirectory(Path.GetDirectoryName(StateFile)!);
        File.WriteAllText(StateFile, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
        return state;
    }

    public static DisplayState? LoadState()
    {
        try
        {
            return File.Exists(StateFile) ? JsonSerializer.Deserialize<DisplayState>(File.ReadAllText(StateFile)) : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Restores the saved resolution and DPI scale, then deletes the state file.</summary>
    public static bool RestoreState(Action<string>? log = null)
    {
        var state = LoadState();
        if (state is null) return false;

        var ok = true;
        try
        {
            var current = GetCurrentMode();
            if (current.Width != state.Width || current.Height != state.Height)
            {
                Apply(state.Width, state.Height, state.Frequency);
                log?.Invoke($"Restored resolution {state.Width}x{state.Height}");
            }
        }
        catch (Exception ex)
        {
            ok = false;
            log?.Invoke($"Failed to restore resolution: {ex.Message}");
        }

        if (state.DpiScale is { } scale)
        {
            try
            {
                if (DpiManager.GetScale().Current != scale)
                {
                    DpiManager.SetScale(scale);
                    log?.Invoke($"Restored display scale {scale}%");
                }
            }
            catch (Exception ex)
            {
                ok = false;
                log?.Invoke($"Failed to restore display scale: {ex.Message}");
            }
        }

        if (ok) File.Delete(StateFile);
        return ok;
    }

    private static void Apply(int width, int height, int frequency)
    {
        var dm = NewDevMode();
        EnumDisplaySettings(PrimaryDevice, ENUM_CURRENT_SETTINGS, ref dm);
        dm.dmPelsWidth = width;
        dm.dmPelsHeight = height;
        dm.dmDisplayFrequency = frequency;
        dm.dmFields = DM_PELSWIDTH | DM_PELSHEIGHT | DM_DISPLAYFREQUENCY;

        var test = ChangeDisplaySettingsEx(PrimaryDevice, ref dm, IntPtr.Zero, CDS_TEST, IntPtr.Zero);
        if (test != DISP_CHANGE_SUCCESSFUL)
            throw new InvalidOperationException($"Display mode {width}x{height}@{frequency}Hz rejected by the driver (code {test}).");

        // Flags = 0: dynamic change, not persisted in the registry.
        var result = ChangeDisplaySettingsEx(PrimaryDevice, ref dm, IntPtr.Zero, 0, IntPtr.Zero);
        if (result != DISP_CHANGE_SUCCESSFUL)
            throw new InvalidOperationException($"Failed to change display mode (code {result}).");

        // Give the shell, DWM and apps time to process WM_DISPLAYCHANGE.
        Thread.Sleep(2000);
    }

    private static DEVMODE NewDevMode() => new() { dmSize = (short)Marshal.SizeOf<DEVMODE>() };
}
