using UIValidator.Core.Model;

namespace UIValidator.Core.Rules;

/// <summary>Elements partially cut off by their container or the window client area.</summary>
public sealed class ClippingRule : IRule
{
    public string Id => "clipping";

    public IEnumerable<Finding> Evaluate(RuleContext ctx)
    {
        foreach (var node in ctx.Tree.Nodes)
        {
            if (node.Id == ctx.Tree.Root.Id || ctx.IsIgnored(node) || ctx.IsInsideScrollContainer(node) || ctx.IsNonClient(node)) continue;

            var clip = ctx.ClipRegion(node);
            var visible = node.Bounds.Intersect(clip);
            if (visible.IsEmpty) continue; // Fully hidden: reported by the offscreen rule.
            if (clip.Contains(node.Bounds, tolerance: 1)) continue;

            var pct = 100.0 * visible.Area / node.Bounds.Area;
            var severity = RuleContext.Interactive.Contains(node.ControlType) || node.ControlType == "Text" ? Severity.Error : Severity.Warning;
            yield return ctx.Create(Id, severity, node,
                $"{node.Label} is clipped: only {pct:0}% of it is visible.", relatedBounds: visible);
        }
    }
}

/// <summary>Elements lying entirely outside their visible region.</summary>
public sealed class OffscreenRule : IRule
{
    public string Id => "offscreen";

    public IEnumerable<Finding> Evaluate(RuleContext ctx)
    {
        foreach (var node in ctx.Tree.Nodes)
        {
            if (node.Id == ctx.Tree.Root.Id || ctx.IsIgnored(node) || ctx.IsInsideScrollContainer(node) || ctx.IsNonClient(node)) continue;
            var interactive = RuleContext.Interactive.Contains(node.ControlType);
            if (!interactive && node.ControlType != "Text") continue;
            if (!node.Bounds.Intersect(ctx.ClipRegion(node)).IsEmpty) continue;

            var finding = ctx.Create(Id, interactive ? Severity.Error : Severity.Warning, node,
                $"{node.Label} is entirely outside the visible area and cannot be reached.");
            // Bounds outside the screenshot are not drawable; keep them only when they intersect the image.
            if (!ctx.WindowBounds.Intersects(node.Bounds)) finding.Bounds = null;
            yield return finding;
        }
    }
}

/// <summary>Sibling controls covering each other.</summary>
public sealed class OverlapRule : IRule
{
    public string Id => "overlap";

    public IEnumerable<Finding> Evaluate(RuleContext ctx)
    {
        var cfg = ctx.Config.Overlap;
        var types = cfg.ControlTypes.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var group in ctx.Tree.Nodes.Where(n => n.ParentId is not null).GroupBy(n => n.ParentId))
        {
            var candidates = group
                .Where(n => !ctx.IsIgnored(n) && !n.IsOffscreen && types.Contains(n.ControlType))
                .Where(n => !n.Bounds.Intersect(ctx.ClipRegion(n)).IsEmpty)
                .ToList();

            for (var i = 0; i < candidates.Count; i++)
                for (var j = i + 1; j < candidates.Count; j++)
                {
                    var a = candidates[i];
                    var b = candidates[j];
                    var overlap = a.Bounds.Intersect(b.Bounds);
                    if (overlap.Width < cfg.MinIntersectionPx || overlap.Height < cfg.MinIntersectionPx) continue;

                    var bothInteractive = a.ControlType != "Text" && b.ControlType != "Text";
                    var f = ctx.Create(Id, bothInteractive ? Severity.Error : Severity.Warning, a,
                        $"{a.Label} overlaps {b.Label} by {overlap.Width}x{overlap.Height}px.", b);
                    yield return f;
                }
        }
    }
}

/// <summary>Clickable targets that are too small to use.</summary>
public sealed class MinSizeRule : IRule
{
    public string Id => "minSize";

    public IEnumerable<Finding> Evaluate(RuleContext ctx)
    {
        var cfg = ctx.Config.MinSize;
        var minW = (int)Math.Round(cfg.MinWidth * ctx.Scale);
        var minH = (int)Math.Round(cfg.MinHeight * ctx.Scale);

        foreach (var node in ctx.Tree.Nodes)
        {
            if (ctx.IsIgnored(node) || node.IsOffscreen || !RuleContext.Interactive.Contains(node.ControlType)) continue;
            if (ctx.IsNonClient(node) || ctx.Tree.AncestorsOf(node).Any(a => a.ControlType == "ScrollBar")) continue;
            if (node.Bounds.Width >= minW && node.Bounds.Height >= minH) continue;

            yield return ctx.Create(Id, Severity.Warning, node,
                $"{node.Label} is {node.Bounds.Width}x{node.Bounds.Height}px; minimum is {minW}x{minH}px at {ctx.Scale * 100:0}% scale.");
        }
    }
}

/// <summary>The window itself must fit inside the monitor work area.</summary>
public sealed class WindowOnScreenRule : IRule
{
    public string Id => "windowOnScreen";

    public IEnumerable<Finding> Evaluate(RuleContext ctx)
    {
        var w = ctx.WindowBounds;
        var a = ctx.WorkArea;
        if (a.Contains(w, tolerance: 1)) yield break;

        var parts = new List<string>();
        if (w.X < a.X) parts.Add($"left {a.X - w.X}px");
        if (w.Y < a.Y) parts.Add($"top {a.Y - w.Y}px");
        if (w.Right > a.Right) parts.Add($"right {w.Right - a.Right}px");
        if (w.Bottom > a.Bottom) parts.Add($"bottom {w.Bottom - a.Bottom}px");

        var root = ctx.Tree.Root;
        var message = $"Window {w.Width}x{w.Height} does not fit the {a.Width}x{a.Height} work area (overflow: {string.Join(", ", parts)}).";
        if (w.Y < a.Y) message += " The title bar is unreachable.";
        var finding = ctx.Create(Id, Severity.Error, root, message);
        finding.Bounds = ctx.ToImage(w.Intersect(a));
        yield return finding;
    }
}
