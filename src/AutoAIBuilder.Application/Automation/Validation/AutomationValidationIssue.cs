namespace AutoAIBuilder.Application.Automation.Validation;

public sealed record AutomationValidationIssue(
    string Stage,
    string Code,
    string Message,
    AutomationValidationSeverity Severity)
{
    public static AutomationValidationIssue Error(
        string stage,
        string code,
        string message) =>
        new(stage, code, message, AutomationValidationSeverity.Error);

    public static AutomationValidationIssue Warning(
        string stage,
        string code,
        string message) =>
        new(stage, code, message, AutomationValidationSeverity.Warning);
}
