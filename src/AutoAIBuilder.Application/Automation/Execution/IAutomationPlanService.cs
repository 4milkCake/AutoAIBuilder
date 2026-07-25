namespace AutoAIBuilder.Application.Automation.Execution;

public interface IAutomationPlanService
{
    Task<AutomationExecutionPlan> CreatePlanAsync(
        AutomationPlanRequest request,
        AutomationExecutionMode mode,
        CancellationToken cancellationToken = default);
}
