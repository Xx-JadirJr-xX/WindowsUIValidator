using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using UIValidator.Core.Config;

namespace UIValidator.Core.Automation;

/// <summary>Executes declarative navigation steps against a running <see cref="AppSession"/>.</summary>
public sealed class StepRunner
{
    private readonly AppSession _session;
    private readonly Action<string> _log;

    public StepRunner(AppSession session, Action<string> log)
    {
        _session = session;
        _log = log;
    }

    public void RunAll(IReadOnlyList<StepConfig> steps)
    {
        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            _log($"step {i + 1}: {step}");
            try
            {
                Run(step);
            }
            catch (Exception ex)
            {
                throw new StepFailedException($"Step {i + 1} ({step}) failed: {ex.Message}", ex);
            }
        }
        if (steps.Count > 0) Thread.Sleep(300);
    }

    public void Run(StepConfig step)
    {
        var timeout = TimeSpan.FromSeconds(step.TimeoutSec);

        if (step.Click is not null)
        {
            // A real mouse click: the Invoke pattern blocks when the button opens a modal dialog.
            Find(step.Click, timeout).Click(moveMouse: true);
        }
        else if (step.DoubleClick is not null)
        {
            Find(step.DoubleClick, timeout).DoubleClick(moveMouse: true);
        }
        else if (step.Focus is not null)
        {
            Find(step.Focus, timeout).Focus();
        }
        else if (step.Type is not null)
        {
            if (step.Type.Target is not null)
            {
                var target = Find(step.Type.Target, timeout);
                target.Focus();
                if (target.Patterns.Value.TryGetPattern(out var value) && !value.IsReadOnly.ValueOrDefault)
                    value.SetValue("");
            }
            Keyboard.Type(step.Type.Text);
        }
        else if (step.Keys is not null)
        {
            Keyboard.TypeSimultaneously(ParseKeys(step.Keys));
        }
        else if (step.WaitFor is not null)
        {
            Find(step.WaitFor, timeout);
        }
        else if (step.Wait is { } ms)
        {
            Thread.Sleep(ms);
        }
        else
        {
            throw new ArgumentException("Step has no action.");
        }

        Wait.UntilInputIsProcessed();
    }

    /// <summary>Finds the first element matching <paramref name="selector"/> in any window of the app.</summary>
    public AutomationElement Find(Selector selector, TimeSpan timeout)
    {
        var condition = BuildCondition(selector);
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            foreach (var window in _session.AllWindows())
            {
                try
                {
                    if (Matches(window, selector)) return window;
                    var found = window.FindFirstDescendant(condition);
                    if (found is not null) return found;
                }
                catch
                {
                    // Window closed while searching.
                }
            }
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"Element [{selector}] not found within {timeout.TotalSeconds:0}s.");
            Thread.Sleep(200);
        }
    }

    private ConditionBase BuildCondition(Selector s)
    {
        var cf = _session.Automation.ConditionFactory;
        var parts = new List<ConditionBase>();
        if (s.AutomationId is not null) parts.Add(cf.ByAutomationId(s.AutomationId));
        if (s.Name is not null) parts.Add(cf.ByName(s.Name));
        if (s.ClassName is not null) parts.Add(cf.ByClassName(s.ClassName));
        if (s.ControlType is not null) parts.Add(cf.ByControlType(ParseControlType(s.ControlType)));
        if (parts.Count == 0) throw new ArgumentException("Selector must define at least one property.");
        return parts.Count == 1 ? parts[0] : new AndCondition(parts.ToArray());
    }

    private static bool Matches(AutomationElement e, Selector s) =>
        (s.AutomationId is null || e.Properties.AutomationId.ValueOrDefault == s.AutomationId) &&
        (s.Name is null || e.Properties.Name.ValueOrDefault == s.Name) &&
        (s.ClassName is null || e.Properties.ClassName.ValueOrDefault == s.ClassName) &&
        (s.ControlType is null || e.Properties.ControlType.ValueOrDefault == ParseControlType(s.ControlType));

    private static ControlType ParseControlType(string name) =>
        Enum.TryParse<ControlType>(name, ignoreCase: true, out var t) ? t : throw new ArgumentException($"Unknown control type '{name}'.");

    /// <summary>Parses chords such as "ctrl+shift+s", "alt+f4" or "enter".</summary>
    public static VirtualKeyShort[] ParseKeys(string chord) =>
        chord.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(ParseKey).ToArray();

    private static VirtualKeyShort ParseKey(string key)
    {
        var k = key.ToLowerInvariant();
        VirtualKeyShort? mapped = k switch
        {
            "ctrl" or "control" => VirtualKeyShort.CONTROL,
            "alt" => VirtualKeyShort.ALT,
            "shift" => VirtualKeyShort.SHIFT,
            "win" => VirtualKeyShort.LWIN,
            "enter" or "return" => VirtualKeyShort.RETURN,
            "esc" or "escape" => VirtualKeyShort.ESCAPE,
            "tab" => VirtualKeyShort.TAB,
            "space" => VirtualKeyShort.SPACE,
            "backspace" => VirtualKeyShort.BACK,
            "delete" or "del" => VirtualKeyShort.DELETE,
            "insert" => VirtualKeyShort.INSERT,
            "home" => VirtualKeyShort.HOME,
            "end" => VirtualKeyShort.END,
            "pageup" => VirtualKeyShort.PRIOR,
            "pagedown" => VirtualKeyShort.NEXT,
            "up" => VirtualKeyShort.UP,
            "down" => VirtualKeyShort.DOWN,
            "left" => VirtualKeyShort.LEFT,
            "right" => VirtualKeyShort.RIGHT,
            _ => null,
        };
        if (mapped is { } m) return m;
        if (k.Length == 1 && char.IsLetterOrDigit(k[0]) && Enum.TryParse<VirtualKeyShort>("KEY_" + char.ToUpperInvariant(k[0]), out var letter))
            return letter;
        if (Enum.TryParse<VirtualKeyShort>(k.ToUpperInvariant(), out var named))
            return named; // F1..F24 and other raw names.
        throw new ArgumentException($"Unknown key '{key}'.");
    }
}

public sealed class StepFailedException(string message, Exception inner) : Exception(message, inner);
