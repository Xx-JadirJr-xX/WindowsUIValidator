using UIValidator.Core.Config;
using UIValidator.Core.Model;
using UIValidator.Core.Reporting;
using UIValidator.Core.Runner;

namespace UIValidator.Tests;

public class ReportTests
{
    private static RunResult SampleRun()
    {
        var scenario = new Scenario { WindowSize = "800x600" };
        var run = new RunResult { AppPath = @"C:\app\App.exe", Screens = ["Main"], Scenarios = [scenario] };
        var result = new ScenarioResult { Scenario = scenario, Screen = "Main" };
        result.Findings.Add(new Finding { Rule = "overlap", Severity = Severity.Error, Message = "</script><b>x</b>" });
        result.ComputeStatus();
        run.Results.Add(result);
        return run;
    }

    [Fact]
    public void Html_embeds_data_and_escapes_script_terminators()
    {
        var html = ReportWriter.RenderHtml(SampleRun(), Path.GetTempPath(), embedImages: false);
        Assert.DoesNotContain("/*__DATA__*/", html);
        Assert.Contains("<\\/script>", html);
        Assert.Equal(2, html.Split("</script>").Length - 1);
    }

    [Fact]
    public void Results_round_trip_through_json()
    {
        var dir = Path.Combine(Path.GetTempPath(), "uiv-" + Guid.NewGuid().ToString("N"));
        ReportWriter.WriteAll(SampleRun(), dir, new OutputConfig());
        var loaded = ReportWriter.LoadResults(dir)!;
        Assert.Single(loaded.Results);
        Assert.Equal(ResultStatus.Failed, loaded.Results[0].Status);
        Assert.Equal("overlap", loaded.Results[0].Findings[0].Rule);
        Directory.Delete(dir, true);
    }

    [Fact]
    public void Matrix_expands_all_combinations()
    {
        var list = MatrixBuilder.Build(new MatrixConfig { Resolutions = ["1920x1080", "1366x768"], DpiScales = [100, 150], WindowSizes = ["maximized"] });
        Assert.Equal(4, list.Count);
        Assert.Equal("1920x1080", list[0].Resolution);
        Assert.Equal(150, list[1].DpiScale);
    }
}
