using UIValidator.Core.Config;
using UIValidator.Core.Model;

namespace UIValidator.Core.Display;

/// <summary>
/// Applies the display part of a scenario (resolution, then scale) and guarantees that the
/// original configuration is restored on dispose, on process exit and after crashes.
/// </summary>
public sealed class EnvironmentController : IDisposable
{
    private readonly Action<string> _log;
    private bool _changed;
    private bool _disposed;

    public EnvironmentController(Action<string> log)
    {
        _log = log;
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
    }

    /// <summary>Restores a state left behind by a crashed run. Returns a warning message when it did.</summary>
    public static string? RecoverPreviousRun(Action<string> log)
    {
        var leftover = DisplayManager.LoadState();
        if (leftover is null) return null;
        log($"Found display state from an interrupted run ({leftover}); restoring it first.");
        DisplayManager.RestoreState(log);
        return $"A previous run was interrupted; the display was restored to {leftover}.";
    }

    /// <summary>Describes the current display, e.g. "1920x1080 @ 60Hz, 125%".</summary>
    public static string Describe()
    {
        var mode = DisplayManager.GetCurrentMode();
        string scale;
        try { scale = DpiManager.GetScale().Current + "%"; }
        catch { scale = "unknown scale"; }
        return $"{mode}, {scale}";
    }

    public void Apply(Scenario scenario)
    {
        if (scenario.Resolution is null && scenario.DpiScale is null) return;
        EnsureStateSaved();

        if (scenario.Resolution is not null && Size2D.TryParse(scenario.Resolution, out var size))
        {
            var current = DisplayManager.GetCurrentMode();
            if (current.Width != size.Width || current.Height != size.Height)
            {
                _log($"  Setting resolution {size}");
                DisplayManager.SetResolution(size);
            }
        }

        if (scenario.DpiScale is { } scale)
        {
            if (DpiManager.GetScale().Current != scale)
            {
                _log($"  Setting display scale {scale}%");
                DpiManager.SetScale(scale);
            }
        }
    }

    private void EnsureStateSaved()
    {
        if (_changed) return;
        int? scale = null;
        try { scale = DpiManager.GetScale().Current; } catch { /* scale API unavailable */ }
        DisplayManager.SaveState(scale);
        _changed = true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        if (_changed) DisplayManager.RestoreState(_log);
    }

    private void OnProcessExit(object? sender, EventArgs e) => Dispose();
}
