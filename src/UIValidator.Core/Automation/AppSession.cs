using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using UIValidator.Core.Config;
using static UIValidator.Core.Native.NativeMethods;

namespace UIValidator.Core.Automation;

/// <summary>A running instance of the application under test, driven through UI Automation.</summary>
public sealed class AppSession : IDisposable
{
    private readonly AppConfig _config;

    public Application App { get; }
    public UIA3Automation Automation { get; }
    public Window MainWindow { get; private set; }

    private AppSession(AppConfig config, Application app, UIA3Automation automation, Window mainWindow)
    {
        _config = config;
        App = app;
        Automation = automation;
        MainWindow = mainWindow;
    }

    /// <summary>Starts the application and waits for its main window.</summary>
    public static AppSession Launch(AppConfig config, string exePath)
    {
        var psi = new ProcessStartInfo(exePath, config.Args)
        {
            WorkingDirectory = config.WorkingDirectory ?? Path.GetDirectoryName(exePath)!,
            UseShellExecute = false,
        };
        var app = Application.Launch(psi);
        var timeout = TimeSpan.FromSeconds(config.StartupTimeoutSec);
        var automation = new UIA3Automation();
        try
        {
            var deadline = DateTime.UtcNow + timeout;
            Window? main = null;
            while (DateTime.UtcNow < deadline && main is null)
            {
                main = FindMainWindow(app, automation, config.MainWindowTitle);
                if (main is null) Thread.Sleep(250);
            }
            if (main is null)
                throw new TimeoutException($"Main window did not appear within {config.StartupTimeoutSec}s.");

            return new AppSession(config, app, automation, main);
        }
        catch
        {
            TryKill(app);
            automation.Dispose();
            throw;
        }
    }

    private static Window? FindMainWindow(Application app, UIA3Automation automation, string? title)
    {
        try
        {
            var windows = app.GetAllTopLevelWindows(automation);
            return windows.FirstOrDefault(w => title is null || (w.Title ?? "").Contains(title, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return null;
        }
    }

    public IntPtr MainHandle => MainWindow.Properties.NativeWindowHandle.ValueOrDefault;

    /// <summary>
    /// Resolves a window spec: "main", "foreground", or a title substring searched among
    /// top-level and modal windows of the application.
    /// </summary>
    public Window FindWindow(string spec, TimeSpan timeout)
    {
        if (spec.Equals("main", StringComparison.OrdinalIgnoreCase))
        {
            if (!Windowing.WindowManager.Exists(MainHandle))
                MainWindow = FindMainWindow(App, Automation, _config.MainWindowTitle) ?? MainWindow;
            return MainWindow;
        }

        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            var found = spec.Equals("foreground", StringComparison.OrdinalIgnoreCase)
                ? Foreground()
                : AllWindows().FirstOrDefault(w => (w.Title ?? "").Contains(spec, StringComparison.OrdinalIgnoreCase));
            if (found is not null) return found;
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"Window '{spec}' not found within {timeout.TotalSeconds:0}s.");
            Thread.Sleep(250);
        }
    }

    private Window? Foreground()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return null;
        var el = Automation.FromHandle(hwnd);
        return el.Properties.ProcessId.ValueOrDefault == App.ProcessId ? el.AsWindow() : null;
    }

    /// <summary>All top-level windows plus their (recursively) modal windows.</summary>
    public IEnumerable<Window> AllWindows()
    {
        var result = new List<Window>();
        void Add(Window w)
        {
            result.Add(w);
            try { foreach (var m in w.ModalWindows) Add(m); } catch { /* window closed meanwhile */ }
        }
        try { foreach (var w in App.GetAllTopLevelWindows(Automation)) Add(w); } catch { }
        return result;
    }

    /// <summary>
    /// Terminates the application. The process under test is disposable, so it is killed
    /// directly: a graceful close would hang on open modal dialogs or "save changes?" prompts.
    /// </summary>
    public void Dispose()
    {
        TryKill(App);
        App.Dispose();
        Automation.Dispose();
    }

    private static void TryKill(Application app)
    {
        try { if (!app.HasExited) app.Kill(); } catch { }
    }
}
