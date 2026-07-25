namespace AutoAIBuilder.Application.Automation.Execution;

public interface IAutomationExecutionService
{
    Task<AutomationExecutionOutcome> ExecuteAsync(
        AutomationExecutionPlan plan,
        Func<AutomationWorkspaceContext, CancellationToken, Task<AutomationAdapterResult>>
            adapter,
        CancellationToken cancellationToken = default);
}
