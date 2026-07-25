namespace AutoAIBuilder.Application.Automation.Validation;

public sealed record AutomationValidationResult(
    IReadOnlyList<AutomationValidationIssue> Issues)
{
    public bool IsValid =>
        Issues.All(issue => issue.Severity != AutomationValidationSeverity.Error);

    public static AutomationValidationResult Success { get; } = new([]);
}
