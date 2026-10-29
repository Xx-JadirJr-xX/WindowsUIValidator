using System.Diagnostics;
using UIValidator.Core.Config;
using UIValidator.Core.Display;
using UIValidator.Core.Reporting;
using UIValidator.Core.Runner;
using UIValidator.Core.Visual;
using UIValidator.Core.Windowing;

namespace UIValidator.Cli;

/// <summary>Command-line entry point.</summary>
internal static class Program
{
    private const string Usage = """
        Windows UI Validator - automated UI testing across resolutions and DPI scales.

        Usage:
          uivalidator run <config.yaml> [options]   Run the scenario matrix and write a report
              --out <dir>         Output directory (default: output.directory/<timestamp>)
              --open              Open report.html when finished
              --update-baselines  Approve all screenshots of this run as new baselines
              --compare <dir|latest>
                                  Mark problems that are new or resolved since a previous run
              --fail-on warning   Also return exit code 1 when only warnings were found
          uivalidator approve <run-dir> [--filter <text>]
                                                    Accept a run's screenshots as visual baselines
          uivalidator validate <config.yaml>        Check a configuration file
          uivalidator report <run-dir>              Regenerate report.html from results.json
          uivalidator displays                      List resolutions and scales of the primary display
          uivalidator restore-display               Restore the display after an interrupted run
          uivalidator --help                        Show this help

        Exit codes: 0 passed (warnings allowed), 1 failures found, 2 invalid config, 3 run errors.
        """;

    private static int Main(string[] args)
    {
        WindowManager.EnsurePerMonitorDpiAwareness();
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            Console.WriteLine(Usage);
            return 0;
        }

        try
        {
            var options = CliArgs.Parse(args.Skip(1));
            return args[0] switch
            {
                "run" => Run(options),
                "validate" => Validate(options),
                "report" => Report(options),
                "approve" => Approve(options),
                "displays" => Displays(),
                "restore-display" => RestoreDisplay(),
                _ => Fail($"Unknown command '{args[0]}'.\n\n{Usage}"),
            };
        }
        catch (OperationCanceledException)
        {
            return Fail("Cancelled.");
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }
    }

    private static ValidatorConfig LoadValid(CliArgs options)
    {
        var path = options.Positional.FirstOrDefault() ?? throw new ArgumentException("Missing config path.");
        var config = ConfigLoader.Load(path);
        var errors = ConfigLoader.Validate(config);
        if (errors.Count > 0)
            throw new InvalidConfigException(errors);
        return config;
    }

    private static int Validate(CliArgs options)
    {
        try
        {
            LoadValid(options);
            Console.WriteLine("Configuration is valid.");
            return 0;
        }
        catch (InvalidConfigException ex)
        {
            foreach (var e in ex.Errors) Console.Error.WriteLine($"error: {e}");
            return 2;
        }
    }

    private static int Run(CliArgs options)
    {
        ValidatorConfig config;
        try { config = LoadValid(options); }
        catch (InvalidConfigException ex)
        {
            foreach (var e in ex.Errors) Console.Error.WriteLine($"error: {e}");
            return 2;
        }

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            // Let the runner unwind so the display configuration is restored.
            e.Cancel = true;
            Console.WriteLine("Cancelling - restoring environment...");
            cts.Cancel();
        };

        var runner = new TestRunner(config, Console.WriteLine) { CompareWith = options.Get("compare") };
        var run = runner.Run(options.Get("out"), cts.Token);

        var report = Path.Combine(runner.RunDirectory!, "report.html");
        Console.WriteLine();
        Console.WriteLine($"Passed: {run.Count(Core.Model.ResultStatus.Passed)}  Warnings: {run.Count(Core.Model.ResultStatus.Warning)}  " +
                          $"Failed: {run.Count(Core.Model.ResultStatus.Failed)}  Errors: {run.Count(Core.Model.ResultStatus.Error)}");
        Console.WriteLine($"Report: {report}");
        Console.WriteLine($"JUnit:  {Path.Combine(runner.RunDirectory!, "junit.xml")}");

        if (options.Has("update-baselines"))
        {
            var approved = new BaselineStore(run.BaselineDirectory!).Approve(run, runner.RunDirectory!);
            Console.WriteLine($"Approved {approved.Count} baseline(s) in {run.BaselineDirectory}");
        }

        if (options.Has("open"))
            Process.Start(new ProcessStartInfo(report) { UseShellExecute = true });

        if (run.ExitCode == 0 && options.Get("fail-on")?.Equals("warning", StringComparison.OrdinalIgnoreCase) == true
            && run.Count(Core.Model.ResultStatus.Warning) > 0)
            return 1;
        return run.ExitCode;
    }

    private static int Report(CliArgs options)
    {
        var dir = options.Positional.FirstOrDefault() ?? throw new ArgumentException("Missing run directory.");
        var run = ReportWriter.LoadResults(dir) ?? throw new FileNotFoundException($"results.json not found in {dir}");
        ReportWriter.WriteAll(run, dir, new OutputConfig { EmbedImages = options.Has("embed") });
        Console.WriteLine($"Report: {Path.Combine(dir, "report.html")}");
        return 0;
    }

    private static int Approve(CliArgs options)
    {
        var dir = options.Positional.FirstOrDefault() ?? throw new ArgumentException("Missing run directory.");
        var run = ReportWriter.LoadResults(dir) ?? throw new FileNotFoundException($"results.json not found in {dir}");
        var baselineDir = options.Get("baselines") ?? run.BaselineDirectory
            ?? throw new ArgumentException("The run has no baseline directory; pass --baselines <dir>.");

        var approved = new BaselineStore(baselineDir).Approve(run, dir, options.Get("filter"));
        foreach (var name in approved) Console.WriteLine($"  approved {name}");
        Console.WriteLine($"Approved {approved.Count} baseline(s) in {baselineDir}");
        return 0;
    }

    private static int Displays()
    {
        Console.WriteLine($"Primary display: {DisplayManager.PrimaryDevice}");
        Console.WriteLine($"Current: {EnvironmentController.Describe()}");
        try
        {
            var scale = DpiManager.GetScale();
            Console.WriteLine($"Scales: {string.Join(", ", DpiManager.GetSupportedScales().Select(s => s + "%"))} (recommended {scale.Recommended}%)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Scales: unavailable ({ex.Message})");
        }
        Console.WriteLine("Resolutions:");
        foreach (var group in DisplayManager.GetSupportedModes().GroupBy(m => (m.Width, m.Height)))
            Console.WriteLine($"  {group.Key.Width}x{group.Key.Height}  ({string.Join(", ", group.Select(m => m.Frequency + "Hz").Distinct())})");
        return 0;
    }

    private static int RestoreDisplay()
    {
        if (DisplayManager.LoadState() is null)
        {
            Console.WriteLine("Nothing to restore.");
            return 0;
        }
        return DisplayManager.RestoreState(Console.WriteLine) ? 0 : 1;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }
}

internal sealed class InvalidConfigException(List<string> errors) : Exception("Invalid configuration.")
{
    public List<string> Errors { get; } = errors;
}

/// <summary>Minimal argument parser: positionals, <c>--flag</c> and <c>--key value</c>.</summary>
internal sealed class CliArgs
{
    private static readonly HashSet<string> Flags = ["open", "embed", "update-baselines", "approve-all"];

    public List<string> Positional { get; } = [];
    private readonly Dictionary<string, string?> _named = new(StringComparer.OrdinalIgnoreCase);

    public static CliArgs Parse(IEnumerable<string> args)
    {
        var result = new CliArgs();
        var list = args.ToList();
        for (var i = 0; i < list.Count; i++)
        {
            var a = list[i];
            if (!a.StartsWith("--")) { result.Positional.Add(a); continue; }
            var key = a[2..];
            if (Flags.Contains(key) || i + 1 >= list.Count || list[i + 1].StartsWith("--"))
                result._named[key] = null;
            else
                result._named[key] = list[++i];
        }
        return result;
    }

    public bool Has(string key) => _named.ContainsKey(key);
    public string? Get(string key) => _named.TryGetValue(key, out var v) ? v : null;
}
