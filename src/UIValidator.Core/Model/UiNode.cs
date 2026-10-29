namespace UIValidator.Core.Model;

/// <summary>
/// A flattened UI Automation element captured from the window under test.
/// Bounds are in screen coordinates (physical pixels).
/// </summary>
public sealed class UiNode
{
    public int Id { get; set; }
    public int? ParentId { get; set; }
    public int Depth { get; set; }
    public string AutomationId { get; set; } = "";
    public string Name { get; set; } = "";
    public string ControlType { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string FrameworkId { get; set; } = "";
    public Rect Bounds { get; set; }
    public bool IsOffscreen { get; set; }
    public bool IsEnabled { get; set; } = true;

    /// <summary>Display label used in reports.</summary>
    public string Label =>
        !string.IsNullOrEmpty(AutomationId) ? $"{ControlType} #{AutomationId}" :
        !string.IsNullOrEmpty(Name) ? $"{ControlType} '{Truncate(Name, 40)}'" :
        $"{ControlType} [{Id}]";

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}

/// <summary>The captured UI tree of a single top-level window.</summary>
public sealed class UiTree
{
    public required UiNode Root { get; init; }
    public required IReadOnlyList<UiNode> Nodes { get; init; }

    private ILookup<int?, UiNode>? _children;
    private Dictionary<int, UiNode>? _byId;

    public IEnumerable<UiNode> ChildrenOf(UiNode node) => (_children ??= Nodes.ToLookup(n => n.ParentId))[node.Id];

    public UiNode? ParentOf(UiNode node) =>
        node.ParentId is { } pid && (_byId ??= Nodes.ToDictionary(n => n.Id)).TryGetValue(pid, out var p) ? p : null;

    public IEnumerable<UiNode> AncestorsOf(UiNode node)
    {
        for (var p = ParentOf(node); p is not null; p = ParentOf(p))
            yield return p;
    }
}
