using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using UIValidator.Core.Model;
using UIValidator.Core.Ocr;

namespace UIValidator.Core.Rules;

/// <summary>
/// Detects captions that do not fit their control.
/// <list type="number">
/// <item>Measurement: the element Name is measured with the configured font at the window DPI and
/// compared with the space available for text (minus control chrome such as check boxes).</item>
/// <item>OCR (optional): the element is read with Windows OCR; a trailing ellipsis or missing words
/// mean the rendered caption is cut.</item>
/// </list>
/// Results are warnings because the real font of a control is not exposed by UI Automation.
/// </summary>
public sealed partial class TextTruncationRule(OcrService? ocr = null) : IRule
{
    public string Id => "textTruncation";

    /// <summary>Horizontal space (logical px) used by control chrome, per control type.</summary>
    private static readonly Dictionary<string, int> Chrome = new()
    {
        ["Button"] = 10,
        ["CheckBox"] = 20,
        ["RadioButton"] = 20,
        ["TabItem"] = 12,
        ["Group"] = 16,
    };

    public IEnumerable<Finding> Evaluate(RuleContext ctx)
    {
        var cfg = ctx.Config.TextTruncation;
        var types = cfg.ControlTypes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        using var font = new Font(cfg.FontFamily, cfg.FontSizePt, GraphicsUnit.Point);

        foreach (var node in ctx.Tree.Nodes)
        {
            if (node.Id == ctx.Tree.Root.Id || ctx.IsIgnored(node) || node.IsOffscreen) continue;
            if (!types.Contains(node.ControlType) || ctx.IsNonClient(node) || ctx.IsInsideScrollContainer(node)) continue;
            var text = Clean(node.Name);
            if (text.Length == 0) continue;
            if (node.Bounds.Intersect(ctx.ClipRegion(node)).IsEmpty) continue;

            var finding = Measure(ctx, node, text, font, cfg.TolerancePx) ?? (ocr is null ? null : ReadBack(ctx, node, text));
            if (finding is not null) yield return finding;
        }
    }

    private Finding? Measure(RuleContext ctx, UiNode node, string text, Font font, int tolerance)
    {
        // Measure in logical (96 DPI) pixels, then compare against logical bounds.
        var logicalWidth = node.Bounds.Width / ctx.Scale;
        var logicalHeight = node.Bounds.Height / ctx.Scale;
        var available = logicalWidth - Chrome.GetValueOrDefault(node.ControlType, 0);
        if (available <= 0) return null;

        // A 96 DPI bitmap context makes measurements independent of this process' DPI.
        using var surface = new Bitmap(1, 1);
        using var g = Graphics.FromImage(surface);
        var single = TextRenderer.MeasureText(g, text, font, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        if (single.Width <= available + tolerance) return null;

        // Multi-line captions: wrap within the available width and compare heights.
        if (logicalHeight >= single.Height * 1.8)
        {
            var wrapped = TextRenderer.MeasureText(g, text, font, new Size((int)available, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
            if (wrapped.Height <= logicalHeight + tolerance) return null;
        }

        var visibleChars = Math.Max(1, (int)(text.Length * available / single.Width));
        return ctx.Create(Id, Severity.Warning, node,
            $"{node.Label} caption needs ~{Math.Ceiling(single.Width * ctx.Scale)}px but only {Math.Floor(available * ctx.Scale)}px are available; " +
            $"likely shown as \"{text[..Math.Min(visibleChars, text.Length)]}…\".");
    }

    private Finding? ReadBack(RuleContext ctx, UiNode node, string text)
    {
        if (ctx.Screenshot is null) return null;
        var region = ctx.ToImage(node.Bounds).Intersect(ctx.ToImage(ctx.ClipRegion(node)));
        if (region.IsEmpty) return null;

        string read;
        try { read = ocr!.Recognize(ctx.Screenshot, region.ToDrawing()); }
        catch { return null; }
        if (read.Length == 0) return null;

        var expectedWords = Words(text);
        var readWords = Words(read);
        // Captions such as "Settings..." legitimately end with an ellipsis.
        var endsWithEllipsis = HasEllipsis(read) && !HasEllipsis(text);
        var missing = expectedWords.Count(w => !readWords.Contains(w));

        // Require a clear signal: an ellipsis, or more than a third of the words missing at the end.
        if (!endsWithEllipsis && !(expectedWords.Count >= 2 && missing * 3 > expectedWords.Count && !readWords.Contains(expectedWords[^1])))
            return null;

        return ctx.Create(Id, Severity.Warning, node, $"{node.Label} caption appears cut on screen: OCR read \"{read.Trim()}\" instead of \"{text}\".");
    }

    private static string Clean(string s) => s.Replace("&", "").Trim();

    private static bool HasEllipsis(string s)
    {
        var t = s.TrimEnd();
        return t.EndsWith("...") || t.EndsWith('…');
    }

    private static List<string> Words(string s) =>
        WordRegex().Matches(s.ToLowerInvariant()).Select(m => m.Value).ToList();

    [GeneratedRegex(@"[\p{L}\p{N}]{2,}")]
    private static partial Regex WordRegex();
}
