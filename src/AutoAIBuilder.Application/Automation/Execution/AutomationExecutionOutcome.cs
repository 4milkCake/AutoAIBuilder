using AutoAIBuilder.Application.Automation.Validation;

namespace AutoAIBuilder.Application.Automation.Execution;

public sealed record AutomationExecutionOutcome(
    Guid AuditId,
    AutomationAuditStatus Status,
    string Summary,
    string? PublishedPath,
    string? RecoveryPath,
    IReadOnlyList<string> OutputPaths,
    IReadOnlyList<AutomationValidationIssue> ValidationIssues,
    bool ReusedPreviousResult)
{
    public bool Succeeded =>
        Status is AutomationAuditStatus.Succeeded
            or AutomationAuditStatus.Reused
            or AutomationAuditStatus.Simulated;
}
