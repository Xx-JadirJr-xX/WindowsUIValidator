using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using UIValidator.Core.Model;

namespace UIValidator.Core.Automation;

/// <summary>Captures a flattened copy of a window's UI Automation tree.</summary>
public static class UiTreeSnapshotter
{
    public static UiTree Snapshot(AutomationElement window, int maxNodes = 5000, int maxDepth = 40)
    {
        var nodes = new List<UiNode>();
        var root = ToNode(window, nodes.Count, null, 0);
        nodes.Add(root);

        var stack = new Stack<(AutomationElement Element, UiNode Node)>();
        stack.Push((window, root));

        while (stack.Count > 0 && nodes.Count < maxNodes)
        {
            var (element, node) = stack.Pop();
            if (node.Depth >= maxDepth) continue;

            AutomationElement[] children;
            try { children = element.FindAllChildren(); }
            catch { continue; }

            // Push in reverse so siblings keep their natural order in the flat list.
            var childNodes = new List<(AutomationElement, UiNode)>();
            foreach (var child in children)
            {
                if (nodes.Count >= maxNodes) break;
                var childNode = ToNode(child, nodes.Count, node.Id, node.Depth + 1);

                // Nested windows (dialogs, popups) are validated separately.
                if (childNode.ControlType == nameof(ControlType.Window)) continue;

                nodes.Add(childNode);
                childNodes.Add((child, childNode));
            }
            for (var i = childNodes.Count - 1; i >= 0; i--) stack.Push(childNodes[i]);
        }

        return new UiTree { Root = root, Nodes = nodes };
    }

    private static UiNode ToNode(AutomationElement e, int id, int? parentId, int depth)
    {
        var p = e.Properties;
        return new UiNode
        {
            Id = id,
            ParentId = parentId,
            Depth = depth,
            AutomationId = Safe(() => p.AutomationId.ValueOrDefault) ?? "",
            Name = Safe(() => p.Name.ValueOrDefault) ?? "",
            ControlType = Safe(() => p.ControlType.ValueOrDefault.ToString()) ?? "Unknown",
            ClassName = Safe(() => p.ClassName.ValueOrDefault) ?? "",
            FrameworkId = Safe(() => p.FrameworkId.ValueOrDefault) ?? "",
            Bounds = Rect.From(Safe(() => p.BoundingRectangle.ValueOrDefault)),
            IsOffscreen = Safe(() => p.IsOffscreen.ValueOrDefault),
            IsEnabled = Safe(() => p.IsEnabled.ValueOrDefault, true),
        };
    }

    private static T? Safe<T>(Func<T> read, T? fallback = default)
    {
        try { return read(); }
        catch { return fallback; }
    }
}
