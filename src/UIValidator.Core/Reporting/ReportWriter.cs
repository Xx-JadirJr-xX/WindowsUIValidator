using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using UIValidator.Core.Config;
using UIValidator.Core.Model;

namespace UIValidator.Core.Reporting;

/// <summary>Writes run artifacts: results.json and the self-contained report.html.</summary>
public static class ReportWriter
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly JsonSerializerOptions CompactOptions = new(JsonOptions) { WriteIndented = false };

    public static void WriteAll(RunResult run, string directory, OutputConfig output)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "results.json"), JsonSerializer.Serialize(run, JsonOptions));
        File.WriteAllText(Path.Combine(directory, "report.html"), RenderHtml(run, directory, output.EmbedImages));
    }

    /// <summary>Injects the run data into the embedded HTML template.</summary>
    public static string RenderHtml(RunResult run, string directory, bool embedImages)
    {
        var json = JsonSerializer.Serialize(run, CompactOptions);
        if (embedImages) json = EmbedImages(json, run, directory);
        // Prevent "</script>" inside data from closing the data block.
        json = json.Replace("</", "<\\/");
        return LoadTemplate().Replace("/*__DATA__*/", json);
    }

    private static string EmbedImages(string json, RunResult run, string directory)
    {
        foreach (var path in run.Results
                     .SelectMany(r => new[] { r.ScreenshotPath, r.BaselinePath, r.DiffPath })
                     .OfType<string>().Distinct())
        {
            var file = Path.Combine(directory, path);
            if (!File.Exists(file)) continue;
            var dataUri = "data:image/png;base64," + Convert.ToBase64String(File.ReadAllBytes(file));
            json = json.Replace($"\"{path}\"", $"\"{dataUri}\"");
        }
        return json;
    }

    public static RunResult? LoadResults(string path)
    {
        var file = Directory.Exists(path) ? Path.Combine(path, "results.json") : path;
        return File.Exists(file) ? JsonSerializer.Deserialize<RunResult>(File.ReadAllText(file), JsonOptions) : null;
    }

    private static string LoadTemplate()
    {
        using var stream = typeof(ReportWriter).Assembly.GetManifestResourceStream("UIValidator.Report.Template")
            ?? throw new InvalidOperationException("Report template resource is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
