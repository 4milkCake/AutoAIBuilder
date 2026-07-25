namespace AutoAIBuilder.Application.Operations;

public sealed record OperationExecution(
    Guid Id,
    string OperationType,
    string DisplayName,
    string ResourceKey,
    Guid? ProjectId,
    OperationExecutionStatus Status,
    int Progress,
    string CurrentStep,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    int TimeoutSeconds,
    string? ErrorCode,
    string? ErrorMessage)
{
    public bool IsTerminal =>
        Status is OperationExecutionStatus.Succeeded
            or OperationExecutionStatus.Cancelled
            or OperationExecutionStatus.TimedOut
            or OperationExecutionStatus.Failed
            or OperationExecutionStatus.Interrupted;
}
