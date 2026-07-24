namespace AutoAIBuilder.Application.Automation;

public sealed record AutomationExecutionResult(
    bool Succeeded,
    string Summary,
    IReadOnlyList<string> Diagnostics);
