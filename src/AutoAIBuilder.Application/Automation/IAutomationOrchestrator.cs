using AutoAIBuilder.Application.Automation.Adapters;
using AutoAIBuilder.Application.Automation.Execution;
using AutoAIBuilder.Application.Automation.Orchestration;

namespace AutoAIBuilder.Application.Automation;

public interface IAutomationOrchestrator
{
    IReadOnlyList<AutomationAdapterDescriptor> GetRegisteredAdapters();

    IReadOnlyList<AutomationIntegrationAssessment> GetRecentAssessments(
        int maximumEntries = 50);

    AutomationIntegrationAssessment AssessIntegration(
        Guid projectId,
        Guid catalogEntryId);

    Task<AutomationExecutionOutcome> ExecutePlanAsync(
        AutomationExecutionPlan plan,
        CancellationToken cancellationToken = default);

    Task<AutomationExecutionResult> ExecuteAsync(
        AutomationRequest request,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);
}
