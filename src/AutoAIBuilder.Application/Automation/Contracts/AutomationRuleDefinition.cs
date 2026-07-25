namespace AutoAIBuilder.Application.Automation.Contracts;

public sealed record AutomationRuleDefinition(
    string Id,
    string Version,
    string Name,
    string Discipline,
    AutomationRuleSeverity Severity,
    string Description,
    string Condition,
    string Source,
    IReadOnlyList<string> SupportedExtensions,
    bool IsEnabled = true);
