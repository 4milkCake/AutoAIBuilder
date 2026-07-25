namespace AutoAIBuilder.Application.Operations;

public interface IOperationCoordinator
{
    Task<OperationExecution> RunAsync(
        OperationRequest request,
        Func<OperationContext, CancellationToken, Task> operation,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    bool Cancel(Guid executionId);

    bool IsResourceBusy(string resourceKey);

    IReadOnlyList<OperationExecution> GetRecent(int maximumEntries = 50);

    int RecoverInterruptedOperations();
}
