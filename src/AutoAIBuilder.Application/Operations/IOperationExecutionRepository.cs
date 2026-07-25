namespace AutoAIBuilder.Application.Operations;

public interface IOperationExecutionRepository
{
    OperationExecution? Get(Guid id);

    IReadOnlyList<OperationExecution> GetRecent(int maximumEntries = 50);

    void Save(OperationExecution execution);

    int MarkIncompleteAsInterrupted(
        DateTimeOffset interruptedAt,
        string reason);
}
