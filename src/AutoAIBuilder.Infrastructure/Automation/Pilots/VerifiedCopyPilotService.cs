using AutoAIBuilder.Application.Automation;
using AutoAIBuilder.Application.Automation.Execution;
using AutoAIBuilder.Application.Automation.Pilots;

namespace AutoAIBuilder.Infrastructure.Automation.Pilots;

public sealed class VerifiedCopyPilotService(
    IAutomationPlanService planService,
    IAutomationOrchestrator orchestrator,
    IAutomationAuditRepository auditRepository,
    TimeProvider? timeProvider = null) : IVerifiedCopyPilotService
{
    private readonly TimeProvider _timeProvider =
        timeProvider ?? TimeProvider.System;

    public async Task<VerifiedCopySimulation> SimulateAsync(
        VerifiedCopyPilotRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sourceFileName = string.IsNullOrWhiteSpace(request.InputPath)
            ? string.Empty
            : Path.GetFileName(request.InputPath);
        var plan = await planService.CreatePlanAsync(
                new AutomationPlanRequest(
                    request.ProjectId,
                    VerifiedCopyPilotContract.CreateRuleCatalog(),
                    VerifiedCopyPilotContract.CreateMask(),
                    [request.InputPath],
                    request.OutputRoot,
                    new Dictionary<string, string>
                    {
                        [VerifiedCopyPilotConstants.SourceFileNameParameter] =
                            sourceFileName
                    }),
                AutomationExecutionMode.Simulation,
                cancellationToken)
            .ConfigureAwait(false);

        if (request.ProjectValidationIssues.Count > 0)
        {
            plan = plan with
            {
                ValidationIssues =
                [
                    .. plan.ValidationIssues,
                    .. request.ProjectValidationIssues
                ]
            };
        }

        var outcome = await orchestrator.ExecutePlanAsync(
                plan,
                cancellationToken)
            .ConfigureAwait(false);
        return new VerifiedCopySimulation(plan, outcome);
    }

    public Task<AutomationExecutionOutcome> ExecuteAsync(
        AutomationExecutionPlan simulatedPlan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(simulatedPlan);
        if (simulatedPlan.Mode != AutomationExecutionMode.Simulation
            || !string.Equals(
                simulatedPlan.Mask.Id,
                VerifiedCopyPilotConstants.MaskId,
                StringComparison.Ordinal)
            || !string.Equals(
                simulatedPlan.Mask.Version,
                VerifiedCopyPilotConstants.MaskVersion,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "O plano não pertence ao piloto de cópia técnica verificada.");
        }

        var simulationAudit = auditRepository.GetLatestByPlanId(
            simulatedPlan.Id);
        if (simulationAudit?.Status != AutomationAuditStatus.Simulated)
        {
            throw new InvalidOperationException(
                "Execute e aprove uma nova simulação antes de aplicar o piloto.");
        }

        var applyPlan = simulatedPlan with
        {
            Id = Guid.NewGuid(),
            Mode = AutomationExecutionMode.Apply,
            Actions = simulatedPlan.Actions
                .Where(action => action.Code != "simulation")
                .ToArray(),
            CreatedAt = _timeProvider.GetUtcNow()
        };
        return orchestrator.ExecutePlanAsync(
            applyPlan,
            cancellationToken);
    }
}
