using UIValidator.Core.Config;
using UIValidator.Core.Model;
using UIValidator.Core.Rules;

namespace UIValidator.Tests;

public class TextTruncationTests
{
    private static RuleContext Context(double scale, params UiNode[] nodes)
    {
        var root = new UiNode { Id = 0, ControlType = "Window", Bounds = new Rect(0, 0, 2000, 1000) };
        foreach (var n in nodes) n.ParentId = 0;
        return new RuleContext
        {
            Tree = new UiTree { Root = root, Nodes = [root, .. nodes] },
            WindowBounds = root.Bounds,
            ClientBounds = root.Bounds,
            WorkArea = root.Bounds,
            Scale = scale,
            Config = new RulesConfig(),
        };
    }

    private static UiNode Label(string text, int width, int height = 20) =>
        new() { Id = 1, ControlType = "Text", Name = text, AutomationId = "lbl", Bounds = new Rect(10, 10, width, height) };

    [Fact]
    public void Narrow_label_is_reported()
    {
        var f = Assert.Single(new TextTruncationRule().Evaluate(Context(1.0, Label("This description is far too long to fit inside its label", 120))));
        Assert.Equal("lbl", f.AutomationId);
    }

    [Fact]
    public void Wide_label_passes()
    {
        Assert.Empty(new TextTruncationRule().Evaluate(Context(1.0, Label("Short", 120))));
    }

    [Fact]
    public void Measurement_accounts_for_dpi()
    {
        // A caption that fits at 100% also fits when both the control and the text are scaled 150%.
        Assert.Empty(new TextTruncationRule().Evaluate(Context(1.5, Label("Medium length caption", 225, 30))));
    }

    [Fact]
    public void Wrapped_multiline_label_that_fits_passes()
    {
        Assert.Empty(new TextTruncationRule().Evaluate(Context(1.0, Label("This text is long but the label is tall enough to wrap it", 150, 80))));
    }
}
