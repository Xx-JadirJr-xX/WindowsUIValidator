using System.Text.Json.Serialization;

namespace UIValidator.Core.Model;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Severity { Info, Warning, Error }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ResultStatus { Passed, Warning, Failed, Error }

/// <summary>A single problem detected by a rule.</summary>
public sealed class Finding
{
    public required string Rule { get; init; }
    public required Severity Severity { get; init; }
    public required string Message { get; init; }
    public string? AutomationId { get; init; }
    public string? ElementName { get; init; }
    public string? ControlType { get; init; }
    public string? ElementLabel { get; init; }

    /// <summary>Element bounds relative to the screenshot (window) origin.</summary>
    public Rect? Bounds { get; set; }

    /// <summary>Secondary rectangle (e.g. the other element in an overlap), relative to the screenshot.</summary>
    public Rect? RelatedBounds { get; set; }
    public string? RelatedLabel { get; init; }

    /// <summary>Stable key used to group the same problem across scenarios.</summary>
    public string Key => $"{Rule}|{AutomationId ?? ElementName ?? ElementLabel}|{RelatedLabel}";
}

/// <summary>One cell of the matrix: environment settings applied before capturing a screen.</summary>
public sealed class Scenario
{
    public string? Resolution { get; init; }
    public int? DpiScale { get; init; }
    public required string WindowSize { get; init; }

    public string Label => $"{Resolution ?? "native"} @ {(DpiScale is { } d ? d + "%" : "native")}, {WindowSize}";

    public string Id => $"{Resolution ?? "native"}_{(DpiScale is { } d ? d.ToString() : "native")}_{WindowSize}";
}

/// <summary>Outcome of validating one screen under one scenario.</summary>
public sealed class ScenarioResult
{
    public required Scenario Scenario { get; init; }
    public required string Screen { get; init; }
    public ResultStatus Status { get; set; }
    public List<Finding> Findings { get; set; } = [];
    public string? ErrorMessage { get; set; }
    public long DurationMs { get; set; }

    /// <summary>Paths relative to the report directory.</summary>
    public string? ScreenshotPath { get; set; }
    public string? BaselinePath { get; set; }
    public string? DiffPath { get; set; }
    public string? TreePath { get; set; }

    /// <summary>Visual similarity against the baseline (0..1), when compared.</summary>
    public double? Similarity { get; set; }

    public Rect? WindowBounds { get; set; }
    public string? ActualResolution { get; set; }
    public int? ActualDpiScale { get; set; }
    public int ElementCount { get; set; }
    public List<string> Log { get; set; } = [];

    /// <summary>Compact copy of the UI tree (bounds relative to the screenshot) for the report viewer.</summary>
    public List<ElementInfo>? Elements { get; set; }

    public void ComputeStatus()
    {
        if (ErrorMessage is not null) Status = ResultStatus.Error;
        else if (Findings.Any(f => f.Severity == Severity.Error)) Status = ResultStatus.Failed;
        else if (Findings.Any(f => f.Severity == Severity.Warning)) Status = ResultStatus.Warning;
        else Status = ResultStatus.Passed;
    }
}

/// <summary>Compact element record embedded in reports.</summary>
public sealed record ElementInfo(int Id, int? ParentId, string ControlType, string AutomationId, string Name, Rect Bounds, bool Offscreen);

/// <summary>Difference between this run and a previous one, keyed by <see cref="Finding.Key"/>.</summary>
public sealed class RunComparison
{
    public string? PreviousRun { get; set; }
    public List<string> NewKeys { get; set; } = [];
    public List<string> ResolvedKeys { get; set; } = [];
}

/// <summary>Whole validation run.</summary>
public sealed class RunResult
{
    public string Tool { get; init; } = "Windows UI Validator";
    public string ToolVersion { get; init; } = typeof(RunResult).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    public required string AppPath { get; init; }
    public string? AppVersion { get; set; }
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.Now;
    public DateTimeOffset FinishedAt { get; set; }
    public string Machine { get; init; } = Environment.MachineName;
    public string OsVersion { get; init; } = Environment.OSVersion.VersionString;
    public string? OriginalDisplay { get; set; }
    public List<string> Screens { get; init; } = [];
    public List<Scenario> Scenarios { get; init; } = [];
    public List<ScenarioResult> Results { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public RunComparison? Comparison { get; set; }

    public int Count(ResultStatus s) => Results.Count(r => r.Status == s);

    /// <summary>Process exit code: 0 all passed (warnings allowed), 1 failures, 3 errors.</summary>
    public int ExitCode => Count(ResultStatus.Error) > 0 ? 3 : Count(ResultStatus.Failed) > 0 ? 1 : 0;
}
