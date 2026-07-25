namespace AutoAIBuilder.Application.Automation.Contracts;

public sealed record AutomationMaskDependency(
    string Id,
    string MinimumVersion,
    bool IsRequired = true);
