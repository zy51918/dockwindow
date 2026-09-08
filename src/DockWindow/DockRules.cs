namespace DockWindow;

public enum RuleAction
{
    Enable,
    Disable,
}

public sealed record DockRule(RuleAction Action, string ProcessPrefix)
{
    public bool Matches(string processName) =>
        ProcessPrefix.Equals("all", StringComparison.OrdinalIgnoreCase) ||
        processName.StartsWith(ProcessPrefix, StringComparison.OrdinalIgnoreCase);

    public static bool TryParse(string? text, out DockRule? rule)
    {
        rule = null;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var parts = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2) return false;

        var action = parts[0].ToLowerInvariant() switch
        {
            "enable" => RuleAction.Enable,
            "disable" => RuleAction.Disable,
            _ => (RuleAction?)null,
        };
        if (action is null) return false;

        var processPrefix = parts[1].Trim();
        if (processPrefix.Length == 0) return false;

        rule = new DockRule(action.Value, processPrefix);
        return true;
    }
}

public sealed class DockPolicy
{
    private readonly IReadOnlyList<DockRule> _rules;

    public IReadOnlyList<DockRule> Rules => _rules;
    public bool HasRules => _rules.Count != 0;
    public bool DefaultAllow { get; }

    public DockPolicy(IEnumerable<DockRule> rules, bool defaultAllow = true)
    {
        _rules = rules.ToArray();
        DefaultAllow = defaultAllow;
    }

    public static DockPolicy FromCommands(IEnumerable<string> commands, bool defaultAllow = true)
    {
        var rules = new List<DockRule>();
        foreach (var command in commands)
        {
            if (DockRule.TryParse(command, out var rule) && rule is not null)
                rules.Add(rule);
        }

        return new DockPolicy(rules, defaultAllow);
    }

    public bool IsAllowed(string processName)
    {
        foreach (var rule in _rules)
        {
            if (rule.Matches(processName))
                return rule.Action == RuleAction.Enable;
        }

        return DefaultAllow;
    }
}
