namespace UIValidator.Core.Config;

/// <summary>Root of the YAML/JSON test configuration.</summary>
public sealed class ValidatorConfig
{
    public AppConfig App { get; set; } = new();
    public MatrixConfig Matrix { get; set; } = new();
    public List<ScreenConfig> Screens { get; set; } = [];
    public RulesConfig Rules { get; set; } = new();
    public OutputConfig Output { get; set; } = new();

    /// <summary>Directory of the config file; relative paths are resolved against it.</summary>
    public string BaseDirectory { get; set; } = Environment.CurrentDirectory;

    public string ResolvePath(string path) =>
        Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(BaseDirectory, path));
}

/// <summary>Application under test.</summary>
public sealed class AppConfig
{
    public string Path { get; set; } = "";
    public string Args { get; set; } = "";
    public string? WorkingDirectory { get; set; }
    public int StartupTimeoutSec { get; set; } = 20;

    /// <summary>Restart the app for every scenario (required for apps that read DPI only at startup).</summary>
    public bool RestartPerScenario { get; set; } = true;

    /// <summary>Optional title (substring) used to pick the main window.</summary>
    public string? MainWindowTitle { get; set; }
}

/// <summary>The scenario matrix: every combination becomes a scenario.</summary>
public sealed class MatrixConfig
{
    /// <summary>Display resolutions such as "1920x1080". Empty means "keep current".</summary>
    public List<string> Resolutions { get; set; } = [];

    /// <summary>Display scale percentages such as 100, 125, 150. Empty means "keep current".</summary>
    public List<int> DpiScales { get; set; } = [];

    /// <summary>Window sizes: "maximized", "current" or "WIDTHxHEIGHT" (physical pixels).</summary>
    public List<string> WindowSizes { get; set; } = ["maximized"];
}

/// <summary>A screen (window state) to reach and validate in every scenario.</summary>
public sealed class ScreenConfig
{
    public string Name { get; set; } = "Main";
    public List<StepConfig> Steps { get; set; } = [];

    /// <summary>Steps executed after validation to return to the initial state (used when the app is not restarted).</summary>
    public List<StepConfig> Teardown { get; set; } = [];

    /// <summary>Which window to validate: "main" (default), "foreground", or a title substring.</summary>
    public string Window { get; set; } = "main";
}

/// <summary>
/// A single navigation step. Exactly one action property should be set, e.g.
/// <c>- click: { automationId: btnSettings }</c>.
/// </summary>
public sealed class StepConfig
{
    public Selector? Click { get; set; }
    public Selector? DoubleClick { get; set; }
    public Selector? Focus { get; set; }
    public TypeStep? Type { get; set; }

    /// <summary>Key chord such as "ctrl+s", "alt+f4", "enter".</summary>
    public string? Keys { get; set; }

    public Selector? WaitFor { get; set; }

    /// <summary>Fixed delay in milliseconds.</summary>
    public int? Wait { get; set; }

    public int TimeoutSec { get; set; } = 10;

    public override string ToString() =>
        Click is not null ? $"click {Click}" :
        DoubleClick is not null ? $"doubleClick {DoubleClick}" :
        Focus is not null ? $"focus {Focus}" :
        Type is not null ? $"type '{Type.Text}' into {Type.Target}" :
        Keys is not null ? $"keys {Keys}" :
        WaitFor is not null ? $"waitFor {WaitFor}" :
        Wait is not null ? $"wait {Wait}ms" : "(empty step)";
}

public sealed class TypeStep
{
    public Selector? Target { get; set; }
    public string Text { get; set; } = "";
}

/// <summary>Identifies a UI element. All non-null properties must match.</summary>
public sealed class Selector
{
    public string? AutomationId { get; set; }
    public string? Name { get; set; }
    public string? ControlType { get; set; }
    public string? ClassName { get; set; }

    public override string ToString() => string.Join(", ", new[]
    {
        AutomationId is null ? null : $"id={AutomationId}",
        Name is null ? null : $"name='{Name}'",
        ControlType is null ? null : $"type={ControlType}",
        ClassName is null ? null : $"class={ClassName}",
    }.Where(s => s is not null));
}

public sealed class RulesConfig
{
    public ToggleRule Clipping { get; set; } = new();
    public OverlapRuleConfig Overlap { get; set; } = new();
    public TextTruncationConfig TextTruncation { get; set; } = new();
    public ToggleRule Offscreen { get; set; } = new();
    public MinSizeConfig MinSize { get; set; } = new();
    public ToggleRule WindowOnScreen { get; set; } = new();
    public VisualDiffConfig VisualDiff { get; set; } = new();

    /// <summary>Elements whose AutomationId is in this list are skipped by every rule.</summary>
    public List<string> IgnoreAutomationIds { get; set; } = [];
}

public class ToggleRule
{
    public bool Enabled { get; set; } = true;
}

public sealed class OverlapRuleConfig : ToggleRule
{
    public int MinIntersectionPx { get; set; } = 4;

    /// <summary>Only these control types are checked for overlap (interactive controls by default).</summary>
    public List<string> ControlTypes { get; set; } =
        ["Button", "Edit", "CheckBox", "RadioButton", "ComboBox", "Slider", "Spinner", "Hyperlink", "ListItem", "Text"];
}

public sealed class TextTruncationConfig : ToggleRule
{
    /// <summary>Also run Windows OCR on each element to detect trailing ellipses.</summary>
    public bool UseOcr { get; set; } = false;

    /// <summary>Tolerance in logical pixels before reporting truncation.</summary>
    public int TolerancePx { get; set; } = 2;

    /// <summary>
    /// Font assumed for measurement. UI Automation does not expose the font of most controls;
    /// "Segoe UI" 9pt is the default of WinForms (.NET 6+), WPF and Win32 dialogs.
    /// </summary>
    public string FontFamily { get; set; } = "Segoe UI";
    public float FontSizePt { get; set; } = 9f;

    /// <summary>Control types whose Name is their visible caption.</summary>
    public List<string> ControlTypes { get; set; } = ["Text", "Button", "CheckBox", "RadioButton", "Hyperlink", "TabItem", "Group"];

    /// <summary>OCR language tag (e.g. "en-US", "pt-BR"); empty uses the user profile languages.</summary>
    public string OcrLanguage { get; set; } = "";
}

public sealed class MinSizeConfig : ToggleRule
{
    /// <summary>Minimum size in logical (96 DPI) pixels.</summary>
    public int MinWidth { get; set; } = 16;
    public int MinHeight { get; set; } = 16;
}

public sealed class VisualDiffConfig : ToggleRule
{
    /// <summary>Minimum similarity (0..1) to pass.</summary>
    public double Threshold { get; set; } = 0.98;

    /// <summary>Per-channel difference (0..255) below which pixels are considered equal.</summary>
    public int PixelTolerance { get; set; } = 16;

    /// <summary>
    /// Maximum fraction (0..1) of changed pixels. Catches small, localized regressions that a
    /// whole-window SSIM dilutes, while tolerating a blinking caret.
    /// </summary>
    public double MaxChangedPixelRatio { get; set; } = 0.002;

    /// <summary>Elements with dynamic content (clocks, carets, animations) excluded from the comparison.</summary>
    public List<string> MaskAutomationIds { get; set; } = [];
}

public sealed class OutputConfig
{
    public string Directory { get; set; } = "ui-results";
    public string BaselineDirectory { get; set; } = "baselines";

    /// <summary>Embed screenshots as base64 in report.html (fully self-contained single file).</summary>
    public bool EmbedImages { get; set; } = true;
}
