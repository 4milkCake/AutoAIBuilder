using AutoAIBuilder.Application.Automation.Validation;

namespace AutoAIBuilder.Application.Automation.Execution;

public interface IAutomationExecutionValidator
{
    Task<IReadOnlyList<AutomationValidationIssue>> ValidateBeforeAsync(
        AutomationExecutionPlan plan,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AutomationValidationIssue>> ValidateAfterAsync(
        AutomationExecutionPlan plan,
        AutomationWorkspaceContext workspace,
        AutomationAdapterResult adapterResult,
        CancellationToken cancellationToken);
}
