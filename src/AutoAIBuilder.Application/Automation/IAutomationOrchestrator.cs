namespace AutoAIBuilder.Application.Automation;

public interface IAutomationOrchestrator
{
    Task<AutomationExecutionResult> ExecuteAsync(
        AutomationRequest request,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);
}
