using UIValidator.Core.Model;

namespace UIValidator.Core.Reporting;

/// <summary>Finds problems introduced or resolved since a previous run.</summary>
public static class RunComparer
{
    /// <summary>Problem identity across runs: the finding key, regardless of scenario.</summary>
    private static HashSet<string> Keys(RunResult run) =>
        run.Results.SelectMany(r => r.Findings).Where(f => f.Severity != Severity.Info).Select(f => f.Key).ToHashSet();

    public static RunComparison Compare(RunResult current, RunResult previous, string previousLabel)
    {
        var now = Keys(current);
        var before = Keys(previous);
        return new RunComparison
        {
            PreviousRun = previousLabel,
            NewKeys = now.Except(before).OrderBy(k => k).ToList(),
            ResolvedKeys = before.Except(now).OrderBy(k => k).ToList(),
        };
    }
}
