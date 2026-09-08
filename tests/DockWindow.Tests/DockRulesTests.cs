using DockWindow;

public class DockRulesTests
{
    [Fact]
    public void Empty_policy_allows_every_process_by_default()
    {
        var policy = new DockPolicy(Array.Empty<DockRule>());

        Assert.True(policy.IsAllowed("notepad.exe"));
        Assert.True(policy.IsAllowed("calc.exe"));
        Assert.False(policy.HasRules);
    }

    [Fact]
    public void Enable_rule_matches_process_name_by_case_insensitive_prefix()
    {
        var policy = DockPolicy.FromCommands(["enable notepad"]);

        Assert.True(policy.IsAllowed("notepad.exe"));
        Assert.True(policy.IsAllowed("NOTEPAD++.EXE"));
        Assert.True(policy.IsAllowed("calc.exe"));
    }

    [Fact]
    public void Disable_rule_blocks_matching_process()
    {
        var policy = DockPolicy.FromCommands(["disable notepad"]);

        Assert.False(policy.IsAllowed("notepad.exe"));
        Assert.True(policy.IsAllowed("calc.exe"));
    }

    [Fact]
    public void All_matches_every_process()
    {
        Assert.True(DockPolicy.FromCommands(["enable all"]).IsAllowed("anything.exe"));
        Assert.False(DockPolicy.FromCommands(["disable all"]).IsAllowed("anything.exe"));
    }

    [Fact]
    public void First_matching_rule_has_priority()
    {
        var policy = DockPolicy.FromCommands([
            "enable notepad",
            "disable all",
        ]);

        Assert.True(policy.IsAllowed("notepad.exe"));
        Assert.False(policy.IsAllowed("calc.exe"));
    }

    [Fact]
    public void A_rule_before_a_specific_rule_wins()
    {
        var policy = DockPolicy.FromCommands([
            "disable all",
            "enable notepad",
        ]);

        Assert.False(policy.IsAllowed("notepad.exe"));
    }

    [Fact]
    public void Unmatched_process_uses_default_allow_value()
    {
        Assert.True(DockPolicy.FromCommands(["enable notepad"]).IsAllowed("calc.exe"));
        Assert.False(DockPolicy.FromCommands(["enable notepad"], defaultAllow: false)
            .IsAllowed("calc.exe"));
    }

    [Fact]
    public void Invalid_commands_are_ignored()
    {
        var policy = DockPolicy.FromCommands([
            "enable",
            "block notepad",
            "disable ",
            "enable notepad extra",
            "  enable   notepad  ",
        ]);

        Assert.True(policy.HasRules);
        Assert.True(policy.IsAllowed("notepad.exe"));
        Assert.True(policy.IsAllowed("calc.exe"));
    }

    [Fact]
    public void TryParse_is_case_insensitive_and_rejects_invalid_commands()
    {
        Assert.True(DockRule.TryParse("DISABLE Notepad", out var rule));
        Assert.Equal(new DockRule(RuleAction.Disable, "Notepad"), rule);
        Assert.False(DockRule.TryParse("enable", out _));
        Assert.False(DockRule.TryParse("disable all extra", out _));
    }
}
