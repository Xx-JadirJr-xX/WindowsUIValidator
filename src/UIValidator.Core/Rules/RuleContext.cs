using System.Drawing;
using UIValidator.Core.Config;
using UIValidator.Core.Model;

namespace UIValidator.Core.Rules;

/// <summary>Everything a rule needs to inspect one captured window.</summary>
public sealed class RuleContext
{
    public required UiTree Tree { get; init; }

    /// <summary>Visible window bounds on screen; the screenshot origin.</summary>
    public required Rect WindowBounds { get; init; }

    /// <summary>Client area of the window on screen.</summary>
    public required Rect ClientBounds { get; init; }

    /// <summary>Work area of the monitor hosting the window.</summary>
    public required Rect WorkArea { get; init; }

    /// <summary>DPI scale factor of the window (1.0 = 96 DPI).</summary>
    public double Scale { get; init; } = 1.0;

    public Bitmap? Screenshot { get; init; }
    public required RulesConfig Config { get; init; }

    /// <summary>Control types that are scroll containers: clipping inside them is expected.</summary>
    public static readonly HashSet<string> ScrollContainers =
        ["List", "Tree", "DataGrid", "Table", "Document", "ScrollBar", "Menu", "ToolBar", "Calendar"];

    /// <summary>Non-client chrome whose bounds lie outside the client area by design.</summary>
    public static readonly HashSet<string> NonClient = ["TitleBar", "MenuBar"];

    public static readonly HashSet<string> Interactive =
        ["Button", "CheckBox", "RadioButton", "Hyperlink", "ComboBox", "Edit", "Slider", "Spinner", "MenuItem", "TabItem", "SplitButton"];

    public bool IsIgnored(UiNode node) =>
        node.Bounds.IsEmpty || (node.AutomationId.Length > 0 && Config.IgnoreAutomationIds.Contains(node.AutomationId));

    public bool IsInsideScrollContainer(UiNode node) => Tree.AncestorsOf(node).Any(a => ScrollContainers.Contains(a.ControlType));

    public bool IsNonClient(UiNode node) =>
        NonClient.Contains(node.ControlType) || Tree.AncestorsOf(node).Any(a => NonClient.Contains(a.ControlType));

    /// <summary>
    /// The region in which <paramref name="node"/> can be seen: the intersection of all ancestor bounds
    /// and, for client elements, the window client area.
    /// </summary>
    public Rect ClipRegion(UiNode node)
    {
        var clip = IsNonClient(node) ? WindowBounds : ClientBounds;
        foreach (var ancestor in Tree.AncestorsOf(node))
        {
            if (ancestor.Id == Tree.Root.Id || ancestor.Bounds.IsEmpty) continue;
            clip = clip.Intersect(ancestor.Bounds);
            if (clip.IsEmpty) break;
        }
        return clip;
    }

    /// <summary>Converts screen coordinates to screenshot coordinates.</summary>
    public Rect ToImage(Rect screen) => screen.Offset(-WindowBounds.X, -WindowBounds.Y);

    public Finding Create(string rule, Severity severity, UiNode node, string message, UiNode? related = null, Rect? relatedBounds = null) => new()
    {
        Rule = rule,
        Severity = severity,
        Message = message,
        AutomationId = NullIfEmpty(node.AutomationId),
        ElementName = NullIfEmpty(node.Name),
        ControlType = node.ControlType,
        ElementLabel = node.Label,
        Bounds = ToImage(node.Bounds),
        RelatedLabel = related?.Label,
        RelatedBounds = relatedBounds is { } rb ? ToImage(rb) : related is null ? null : ToImage(related.Bounds),
    };

    private static string? NullIfEmpty(string s) => s.Length == 0 ? null : s;
}

/// <summary>A layout validation rule.</summary>
public interface IRule
{
    string Id { get; }
    IEnumerable<Finding> Evaluate(RuleContext context);
}
