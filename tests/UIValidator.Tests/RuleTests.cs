using UIValidator.Core.Config;
using UIValidator.Core.Model;
using UIValidator.Core.Rules;

namespace UIValidator.Tests;

public class RuleTests
{
    private static readonly Rect Window = new(0, 0, 800, 600);
    private static readonly Rect Client = new(0, 30, 800, 570);

    private static RuleContext Context(params UiNode[] children)
    {
        var root = new UiNode { Id = 0, ControlType = "Window", Bounds = Window };
        var nodes = new List<UiNode> { root };
        foreach (var c in children)
        {
            c.ParentId ??= 0;
            nodes.Add(c);
        }
        return new RuleContext
        {
            Tree = new UiTree { Root = root, Nodes = nodes },
            WindowBounds = Window,
            ClientBounds = Client,
            WorkArea = new Rect(0, 0, 1920, 1040),
            Config = new RulesConfig(),
        };
    }

    private static UiNode Node(int id, string type, Rect bounds, string aid = "", int? parent = null) =>
        new() { Id = id, ParentId = parent, ControlType = type, Bounds = bounds, AutomationId = aid };

    [Fact]
    public void Clipping_detects_partially_visible_button()
    {
        var ctx = Context(Node(1, "Button", new Rect(750, 100, 100, 30), "btnOk"));
        var f = Assert.Single(new ClippingRule().Evaluate(ctx));
        Assert.Equal("btnOk", f.AutomationId);
        Assert.Equal(Severity.Error, f.Severity);
    }

    [Fact]
    public void Clipping_ignores_fully_visible_and_scroll_container_items()
    {
        var ctx = Context(
            Node(1, "Button", new Rect(10, 100, 100, 30)),
            Node(2, "List", new Rect(10, 200, 200, 100)),
            Node(3, "ListItem", new Rect(10, 280, 200, 40), parent: 2));
        Assert.Empty(new ClippingRule().Evaluate(ctx));
    }

    [Fact]
    public void Clipping_ignores_non_client_chrome()
    {
        var ctx = Context(
            Node(1, "TitleBar", new Rect(-8, 0, 820, 30)),
            Node(2, "Button", new Rect(780, 0, 46, 30), "Close", parent: 1));
        Assert.Empty(new ClippingRule().Evaluate(ctx));
    }

    [Fact]
    public void Offscreen_detects_unreachable_button()
    {
        var ctx = Context(Node(1, "Button", new Rect(3000, 3000, 80, 30), "btnGhost"));
        var f = Assert.Single(new OffscreenRule().Evaluate(ctx));
        Assert.Null(f.Bounds);
    }

    [Fact]
    public void Overlap_detects_siblings_but_not_parent_child()
    {
        var ctx = Context(
            Node(1, "Button", new Rect(20, 500, 120, 32), "a"),
            Node(2, "Button", new Rect(100, 510, 120, 32), "b"),
            Node(3, "Pane", new Rect(300, 100, 300, 300)),
            Node(4, "Button", new Rect(310, 110, 50, 20), "child", parent: 3));
        var f = Assert.Single(new OverlapRule().Evaluate(ctx));
        Assert.Equal("a", f.AutomationId);
        Assert.Contains("#b", f.RelatedLabel);
    }

    [Fact]
    public void Composite_control_parts_are_ignored()
    {
        var ctx = Context(
            Node(1, "ComboBox", new Rect(20, 60, 200, 23)),
            Node(2, "Text", new Rect(22, 62, 180, 19), parent: 1),
            Node(3, "Button", new Rect(200, 61, 15, 21), parent: 1));
        Assert.Empty(new OverlapRule().Evaluate(ctx));
        Assert.Empty(new MinSizeRule().Evaluate(ctx));
    }

    [Fact]
    public void Key_chords_are_parsed()
    {
        var keys = UIValidator.Core.Automation.StepRunner.ParseKeys("ctrl+shift+s");
        Assert.Equal(3, keys.Length);
        Assert.Single(UIValidator.Core.Automation.StepRunner.ParseKeys("F4"));
        Assert.Throws<ArgumentException>(() => UIValidator.Core.Automation.StepRunner.ParseKeys("ctrl+nope"));
    }

    [Fact]
    public void MinSize_scales_with_dpi()
    {
        var root = Context(Node(1, "Button", new Rect(10, 100, 20, 20)));
        Assert.Empty(new MinSizeRule().Evaluate(root));

        var scaled = new RuleContext
        {
            Tree = root.Tree, WindowBounds = Window, ClientBounds = Client, WorkArea = root.WorkArea,
            Config = new RulesConfig(), Scale = 2.0,
        };
        Assert.Single(new MinSizeRule().Evaluate(scaled));
    }

    [Fact]
    public void WindowOnScreen_detects_overflow()
    {
        var ctx = new RuleContext
        {
            Tree = Context().Tree,
            WindowBounds = new Rect(0, -10, 2000, 600),
            ClientBounds = Client,
            WorkArea = new Rect(0, 0, 1920, 1040),
            Config = new RulesConfig(),
        };
        var f = Assert.Single(new WindowOnScreenRule().Evaluate(ctx));
        Assert.Contains("title bar", f.Message);
    }
}
