namespace AutoAIBuilder.Application.Automation.Execution;

public sealed record AutomationAuditEntry(
    Guid Id,
    Guid PlanId,
    Guid ProjectId,
    string MaskId,
    string MaskVersion,
    AutomationExecutionMode Mode,
    AutomationAuditStatus Status,
    string IdempotencyKey,
    IReadOnlyList<AutomationInputSnapshot> Inputs,
    IReadOnlyList<string> OutputPaths,
    string? PublishedPath,
    string? RecoveryPath,
    string Summary,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);
