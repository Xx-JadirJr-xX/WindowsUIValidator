using UIValidator.Core.Config;
using UIValidator.Core.Model;

namespace UIValidator.Core.Rules;

/// <summary>Runs every enabled rule and collects findings.</summary>
public sealed class RuleEngine
{
    private readonly List<IRule> _rules;

    public RuleEngine(IEnumerable<IRule> rules) => _rules = rules.ToList();

    public IReadOnlyList<IRule> Rules => _rules;

    /// <summary>Builds the rule set enabled in the configuration.</summary>
    public static RuleEngine FromConfig(RulesConfig config)
    {
        var rules = new List<IRule>();
        if (config.WindowOnScreen.Enabled) rules.Add(new WindowOnScreenRule());
        if (config.Clipping.Enabled) rules.Add(new ClippingRule());
        if (config.Offscreen.Enabled) rules.Add(new OffscreenRule());
        if (config.Overlap.Enabled) rules.Add(new OverlapRule());
        if (config.MinSize.Enabled) rules.Add(new MinSizeRule());
        return new RuleEngine(rules);
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
