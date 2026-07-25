namespace AutoAIBuilder.Application.Operations;

public sealed class OperationContext
{
    private readonly Action<OperationProgress> _report;

    internal OperationContext(
        Guid executionId,
        Action<OperationProgress> report)
    {
        ExecutionId = executionId;
        _report = report;
    }

    public Guid ExecutionId { get; }

    public void Report(int percentage, string message) =>
        _report(OperationProgress.Create(percentage, message));
}
