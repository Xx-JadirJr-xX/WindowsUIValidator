using System.Text.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace UIValidator.Core.Config;

/// <summary>Loads and validates <see cref="ValidatorConfig"/> from YAML or JSON files.</summary>
public static class ConfigLoader
{
    public static ValidatorConfig Load(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Config file not found: {fullPath}");

        var text = File.ReadAllText(fullPath);
        var config = Path.GetExtension(fullPath).Equals(".json", StringComparison.OrdinalIgnoreCase)
            ? JsonSerializer.Deserialize<ValidatorConfig>(text, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            : Parse(text);

        config ??= new ValidatorConfig();
        config.BaseDirectory = Path.GetDirectoryName(fullPath)!;
        return config;
    }

    public static ValidatorConfig Parse(string yaml) =>
        new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build()
            .Deserialize<ValidatorConfig>(yaml) ?? new ValidatorConfig();

    /// <summary>Returns a list of human-readable problems; empty when the config is valid.</summary>
    public static List<string> Validate(ValidatorConfig config)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(config.App.Path))
            errors.Add("app.path is required.");
        else if (!File.Exists(config.ResolvePath(config.App.Path)))
            errors.Add($"app.path does not exist: {config.ResolvePath(config.App.Path)}");

        foreach (var r in config.Matrix.Resolutions)
            if (!Size2D.TryParse(r, out _))
                errors.Add($"Invalid resolution '{r}'. Expected WIDTHxHEIGHT.");

        foreach (var w in config.Matrix.WindowSizes)
            if (w is not ("maximized" or "current") && !Size2D.TryParse(w, out _))
                errors.Add($"Invalid window size '{w}'. Expected 'maximized', 'current' or WIDTHxHEIGHT.");

        foreach (var d in config.Matrix.DpiScales)
            if (!Display.DpiManager.Steps.Contains(d))
                errors.Add($"Invalid DPI scale {d}. Expected one of {string.Join(", ", Display.DpiManager.Steps)}.");

        if (config.Screens.Count == 0)
            errors.Add("At least one entry in 'screens' is required.");

        if (config.Rules.VisualDiff.Threshold is < 0 or > 1)
            errors.Add("rules.visualDiff.threshold must be between 0 and 1.");
        if (config.Rules.VisualDiff.MaxChangedPixelRatio is < 0 or > 1)
            errors.Add("rules.visualDiff.maxChangedPixelRatio must be between 0 and 1.");

        return errors;
    }
}

/// <summary>Width/height pair parsed from strings like "1920x1080".</summary>
public readonly record struct Size2D(int Width, int Height)
{
    public static bool TryParse(string? text, out Size2D size)
    {
        size = default;
        var parts = text?.ToLowerInvariant().Split('x', StringSplitOptions.TrimEntries);
        if (parts is not { Length: 2 } || !int.TryParse(parts[0], out var w) || !int.TryParse(parts[1], out var h) || w <= 0 || h <= 0)
            return false;
        size = new Size2D(w, h);
        return true;
    }

    public override string ToString() => $"{Width}x{Height}";
}
