using AutoAIBuilder.Application.Automation.Execution;

namespace AutoAIBuilder.Application.Automation.Adapters;

public interface IAutomationAdapter
{
    AutomationAdapterDescriptor Descriptor { get; }

    Task<AutomationAdapterResult> ExecuteAsync(
        AutomationWorkspaceContext context,
        CancellationToken cancellationToken = default);
}
