using UIValidator.Core.Model;

namespace UIValidator.Core.Visual;

/// <summary>
/// Approved reference screenshots, one PNG per scenario/screen, stored in the baseline directory
/// (commit it to source control to share baselines with the team and CI).
/// </summary>
public sealed class BaselineStore(string directory)
{
    public string Directory { get; } = directory;

    public string PathFor(string artifactName) => Path.Combine(Directory, artifactName + ".png");

    public bool Exists(string artifactName) => File.Exists(PathFor(artifactName));

    /// <summary>
    /// Copies the screenshots of a run into the baseline directory.
    /// </summary>
    /// <param name="filter">Optional substring matched against "scenario__screen" names.</param>
    /// <returns>Names of the approved baselines.</returns>
    public List<string> Approve(RunResult run, string runDirectory, string? filter = null)
    {
        System.IO.Directory.CreateDirectory(Directory);
        var approved = new List<string>();
        foreach (var result in run.Results)
        {
            if (result.ScreenshotPath is null || result.ErrorMessage is not null) continue;
            var name = Path.GetFileNameWithoutExtension(result.ScreenshotPath);
            if (filter is not null && !name.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;

            var source = Path.Combine(runDirectory, result.ScreenshotPath);
            if (!File.Exists(source)) continue;
            File.Copy(source, PathFor(name), overwrite: true);
            approved.Add(name);
        }
        return approved;
    }
}
