using AutoAIBuilder.Application.Automation.Contracts;
using AutoAIBuilder.Application.Automation.Validation;

namespace AutoAIBuilder.Application.Automation.Execution;

public sealed record AutomationExecutionPlan(
    Guid Id,
    Guid ProjectId,
    AutomationExecutionMode Mode,
    AutomationMaskDefinition Mask,
    IReadOnlyList<AutomationInputSnapshot> Inputs,
    string OutputRoot,
    IReadOnlyDictionary<string, string> Parameters,
    IReadOnlyList<AutomationPlanAction> Actions,
    IReadOnlyList<AutomationValidationIssue> ValidationIssues,
    string IdempotencyKey,
    DateTimeOffset CreatedAt)
{
    public bool IsValid =>
        ValidationIssues.All(
            issue => issue.Severity != AutomationValidationSeverity.Error);
}
