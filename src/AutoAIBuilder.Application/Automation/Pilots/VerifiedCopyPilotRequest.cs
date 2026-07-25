using AutoAIBuilder.Application.Automation.Validation;

namespace AutoAIBuilder.Application.Automation.Pilots;

public sealed record VerifiedCopyPilotRequest(
    Guid ProjectId,
    string InputPath,
    string OutputRoot,
    IReadOnlyList<AutomationValidationIssue> ProjectValidationIssues);
