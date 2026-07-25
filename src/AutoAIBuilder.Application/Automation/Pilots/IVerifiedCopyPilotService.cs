using AutoAIBuilder.Application.Automation.Execution;

namespace AutoAIBuilder.Application.Automation.Pilots;

public interface IVerifiedCopyPilotService
{
    Task<VerifiedCopySimulation> SimulateAsync(
        VerifiedCopyPilotRequest request,
        CancellationToken cancellationToken = default);

    Task<AutomationExecutionOutcome> ExecuteAsync(
        AutomationExecutionPlan simulatedPlan,
        CancellationToken cancellationToken = default);
}
