using AutoAIBuilder.Application.Automation.Contracts;

namespace AutoAIBuilder.Application.Automation.Execution;

public sealed record AutomationPlanRequest(
    Guid ProjectId,
    AutomationRuleCatalog RuleCatalog,
    AutomationMaskDefinition Mask,
    IReadOnlyList<string> InputPaths,
    string OutputRoot,
    IReadOnlyDictionary<string, string> Parameters,
    IReadOnlyDictionary<string, string>? AvailableDependencies = null);
