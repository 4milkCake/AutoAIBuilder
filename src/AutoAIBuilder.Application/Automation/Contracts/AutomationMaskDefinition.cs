namespace AutoAIBuilder.Application.Automation.Contracts;

public sealed record AutomationMaskDefinition(
    string SchemaVersion,
    string Id,
    string Version,
    string Name,
    string Discipline,
    string Description,
    string MinimumApplicationVersion,
    IReadOnlyList<string> AcceptedExtensions,
    IReadOnlyList<string> RequiredRuleIds,
    IReadOnlyList<AutomationMaskDependency> Dependencies,
    IReadOnlyList<AutomationMaskParameter> Parameters,
    IReadOnlyList<AutomationMaskOutput> Outputs,
    IReadOnlyList<AutomationValidationRequirement> Preconditions,
    IReadOnlyList<AutomationValidationRequirement> Postconditions,
    bool SupportsSimulation = true,
    bool IsIdempotent = true);
