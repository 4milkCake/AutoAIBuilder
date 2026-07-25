namespace AutoAIBuilder.Application.Operations;

public enum OperationExecutionStatus
{
    Pending,
    Running,
    Succeeded,
    Cancelled,
    TimedOut,
    Failed,
    Interrupted
}
