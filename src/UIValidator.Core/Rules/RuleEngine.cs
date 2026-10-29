using UIValidator.Core.Config;
using UIValidator.Core.Model;

namespace UIValidator.Core.Rules;

/// <summary>Runs every enabled rule and collects findings.</summary>
public sealed class RuleEngine
{
    private readonly List<IRule> _rules;

    public RuleEngine(IEnumerable<IRule> rules) => _rules = rules.ToList();

    public IReadOnlyList<IRule> Rules => _rules;

    /// <summary>Setup problems to surface in the report (e.g. OCR unavailable).</summary>
    public List<string> Warnings { get; private init; } = [];

    /// <summary>Builds the rule set enabled in the configuration.</summary>
    public static RuleEngine FromConfig(RulesConfig config)
    {
        var rules = new List<IRule>();
        var warnings = new List<string>();
        if (config.WindowOnScreen.Enabled) rules.Add(new WindowOnScreenRule());
        if (config.Clipping.Enabled) rules.Add(new ClippingRule());
        if (config.Offscreen.Enabled) rules.Add(new OffscreenRule());
        if (config.Overlap.Enabled) rules.Add(new OverlapRule());
        if (config.MinSize.Enabled) rules.Add(new MinSizeRule());
        if (config.TextTruncation.Enabled)
        {
            var ocr = config.TextTruncation.UseOcr ? Ocr.OcrService.TryCreate(config.TextTruncation.OcrLanguage) : null;
            if (config.TextTruncation.UseOcr && ocr is null)
                warnings.Add("OCR was requested but no Windows OCR language is available; using text measurement only.");
            rules.Add(new TextTruncationRule(ocr));
        }
        return new RuleEngine(rules) { Warnings = warnings };
    }

    public void Add(IRule rule) => _rules.Add(rule);

    public List<Finding> Evaluate(RuleContext context)
    {
        var findings = new List<Finding>();
        foreach (var rule in _rules)
        {
            try
            {
                findings.AddRange(rule.Evaluate(context));
            }
            catch (Exception ex)
            {
                findings.Add(new Finding
                {
                    Rule = rule.Id,
                    Severity = Severity.Warning,
                    Message = $"Rule '{rule.Id}' failed: {ex.Message}",
                });
            }
        }
        return findings;
    }
}
