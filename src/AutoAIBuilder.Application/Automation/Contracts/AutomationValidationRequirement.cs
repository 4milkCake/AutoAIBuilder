namespace AutoAIBuilder.Application.Automation.Contracts;

public sealed record AutomationValidationRequirement(
    string Id,
    string Description,
    bool IsBlocking = true);
