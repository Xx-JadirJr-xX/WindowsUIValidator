using System.Globalization;
using System.Xml.Linq;
using UIValidator.Core.Model;

namespace UIValidator.Core.Reporting;

/// <summary>
/// Writes results as JUnit XML, understood by GitHub Actions, Azure DevOps, GitLab and Jenkins.
/// One test suite per screen, one test case per scenario. Warnings do not fail a case and are
/// reported in system-out.
/// </summary>
public static class JUnitWriter
{
    public static XDocument Build(RunResult run)
    {
        var inv = CultureInfo.InvariantCulture;
        var suites = new XElement("testsuites", new XAttribute("name", run.Tool));

        foreach (var group in run.Results.GroupBy(r => r.Screen))
        {
            var suite = new XElement("testsuite",
                new XAttribute("name", group.Key),
                new XAttribute("tests", group.Count()),
                new XAttribute("failures", group.Count(r => r.Status == ResultStatus.Failed)),
                new XAttribute("errors", group.Count(r => r.Status == ResultStatus.Error)),
                new XAttribute("time", (group.Sum(r => r.DurationMs) / 1000.0).ToString("0.000", inv)),
                new XAttribute("timestamp", run.StartedAt.ToString("s", inv)));

            foreach (var r in group)
            {
                var testCase = new XElement("testcase",
                    new XAttribute("classname", $"{group.Key}"),
                    new XAttribute("name", r.Scenario.Label),
                    new XAttribute("time", (r.DurationMs / 1000.0).ToString("0.000", inv)));

                var errors = r.Findings.Where(f => f.Severity == Severity.Error).ToList();
                if (r.Status == ResultStatus.Error)
                    testCase.Add(new XElement("error", new XAttribute("message", r.ErrorMessage ?? "error"), string.Join("\n", r.Log)));
                else if (errors.Count > 0)
                    testCase.Add(new XElement("failure",
                        new XAttribute("message", $"{errors.Count} layout error(s): {errors[0].Message}"),
                        string.Join("\n", errors.Select(f => $"[{f.Rule}] {f.Message}"))));

                var others = r.Findings.Where(f => f.Severity != Severity.Error).ToList();
                if (others.Count > 0)
                    testCase.Add(new XElement("system-out", string.Join("\n", others.Select(f => $"{f.Severity}: [{f.Rule}] {f.Message}"))));

                suite.Add(testCase);
            }
            suites.Add(suite);
        }

        return new XDocument(new XDeclaration("1.0", "utf-8", null), suites);
    }

    public static void Write(RunResult run, string path) => Build(run).Save(path);
}
