using AutoAIBuilder.Application.Automation.Execution;

namespace AutoAIBuilder.Application.Automation.Pilots;

public sealed record VerifiedCopySimulation(
    AutomationExecutionPlan Plan,
    AutomationExecutionOutcome Outcome);
