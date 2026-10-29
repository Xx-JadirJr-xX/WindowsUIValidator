using System.Diagnostics;
using System.Drawing.Imaging;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using UIValidator.Core.Automation;
using UIValidator.Core.Capture;
using UIValidator.Core.Config;
using UIValidator.Core.Model;
using UIValidator.Core.Reporting;
using UIValidator.Core.Rules;
using UIValidator.Core.Windowing;

namespace UIValidator.Core.Runner;

/// <summary>Orchestrates a full validation run over the scenario matrix.</summary>
public sealed class TestRunner
{
    private readonly ValidatorConfig _config;
    private readonly Action<string> _log;
    private readonly RuleEngine _rules;

    public TestRunner(ValidatorConfig config, Action<string>? log = null)
    {
        _config = config;
        _log = log ?? (_ => { });
        _rules = RuleEngine.FromConfig(config.Rules);
    }

    /// <summary>Directory where the last run wrote its artifacts.</summary>
    public string? RunDirectory { get; private set; }

    public RunResult Run(string? outputDirectory = null, CancellationToken cancellation = default)
    {
        var exe = _config.ResolvePath(_config.App.Path);
        var scenarios = MatrixBuilder.Build(_config.Matrix);
        var run = new RunResult
        {
            AppPath = exe,
            AppVersion = FileVersionInfo.GetVersionInfo(exe).FileVersion,
            Screens = _config.Screens.Select(s => s.Name).ToList(),
            Scenarios = scenarios,
        };

        RunDirectory = outputDirectory ?? Path.Combine(_config.ResolvePath(_config.Output.Directory), run.StartedAt.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(Path.Combine(RunDirectory, "images"));
        Directory.CreateDirectory(Path.Combine(RunDirectory, "trees"));

        _log($"Running {scenarios.Count} scenario(s) x {_config.Screens.Count} screen(s) against {exe}");
        _log($"Output: {RunDirectory}");

        AppSession? session = null;
        try
        {
            foreach (var scenario in scenarios)
            {
                cancellation.ThrowIfCancellationRequested();
                _log($"Scenario {scenario.Label}");

                foreach (var screen in _config.Screens)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var result = RunScreen(scenario, screen, exe, ref session);
                    run.Results.Add(result);
                    _log($"  {screen.Name,-20} {result.Status,-8} {result.Findings.Count} finding(s){(result.ErrorMessage is null ? "" : " - " + result.ErrorMessage)}");

                    // Fresh process per screen guarantees a clean state and up-to-date DPI.
                    if (_config.App.RestartPerScenario)
                    {
                        session?.Dispose();
                        session = null;
                    }
                }
            }
        }
        finally
        {
            session?.Dispose();
            run.FinishedAt = DateTimeOffset.Now;
            ReportWriter.WriteAll(run, RunDirectory, _config.Output);
        }

        return run;
    }

    private ScenarioResult RunScreen(Scenario scenario, ScreenConfig screen, string exe, ref AppSession? session)
    {
        var sw = Stopwatch.StartNew();
        var result = new ScenarioResult { Scenario = scenario, Screen = screen.Name };
        Window? target = null;
        try
        {
            session ??= AppSession.Launch(_config.App, exe);

            var mainHandle = session.MainHandle;
            WindowManager.ApplySize(mainHandle, scenario.WindowSize);
            WindowManager.BringToFront(mainHandle);
            Thread.Sleep(300);

            target = session.FindWindow(screen.Window, TimeSpan.FromSeconds(10));
            var hwnd = target.Properties.NativeWindowHandle.ValueOrDefault;
            WindowManager.BringToFront(hwnd);

            Validate(result, target, hwnd);
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            result.Log.Add(ex.ToString());
            // A broken session would poison the next screens; start fresh.
            session?.Dispose();
            session = null;
        }
        finally
        {
            if (session is not null)
                ResetScreen(session, screen, target);
            result.DurationMs = sw.ElapsedMilliseconds;
            result.ComputeStatus();
        }
        return result;
    }

    private void Validate(ScenarioResult result, Window window, IntPtr hwnd)
    {
        using var screenshot = WindowCapture.CaptureStable(hwnd, out var bounds);
        var tree = UiTreeSnapshotter.Snapshot(window);

        var context = new RuleContext
        {
            Tree = tree,
            WindowBounds = bounds,
            ClientBounds = WindowManager.GetClientBounds(hwnd),
            WorkArea = WindowManager.GetWorkArea(hwnd),
            Scale = WindowManager.GetScale(hwnd),
            Screenshot = screenshot,
            Config = _config.Rules,
        };
        result.Findings.AddRange(_rules.Evaluate(context));

        result.WindowBounds = bounds;
        result.ActualResolution = WindowManager.GetMonitorResolution(hwnd);
        result.ActualDpiScale = WindowManager.GetMonitorScalePercent(hwnd);
        result.ElementCount = tree.Nodes.Count;
        result.Elements = tree.Nodes
            .Select(n => new ElementInfo(n.Id, n.ParentId, n.ControlType, n.AutomationId, n.Name, context.ToImage(n.Bounds), n.IsOffscreen))
            .ToList();

        var baseName = ArtifactName(result);
        result.ScreenshotPath = $"images/{baseName}.png";
        screenshot.Save(Path.Combine(RunDirectory!, result.ScreenshotPath), ImageFormat.Png);

        result.TreePath = $"trees/{baseName}.json";
        File.WriteAllText(Path.Combine(RunDirectory!, result.TreePath), JsonSerializer.Serialize(tree.Nodes, ReportWriter.JsonOptions));
    }

    /// <summary>Brings the app back to its initial state when it is reused across screens.</summary>
    private void ResetScreen(AppSession session, ScreenConfig screen, Window? target)
    {
        if (_config.App.RestartPerScenario) return;
        try
        {
            if (target is not null && target.Properties.NativeWindowHandle.ValueOrDefault != session.MainHandle)
                target.Close();
        }
        catch (Exception ex)
        {
            _log($"  teardown failed: {ex.Message}");
        }
    }

    public static string ArtifactName(ScenarioResult r) => Sanitize($"{r.Scenario.Id}__{r.Screen}");

    private static string Sanitize(string s) =>
        string.Concat(s.Select(c => Path.GetInvalidFileNameChars().Contains(c) || c == ' ' ? '_' : c));
}
