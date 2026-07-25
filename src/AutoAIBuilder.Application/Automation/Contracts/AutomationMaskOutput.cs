namespace AutoAIBuilder.Application.Automation.Contracts;

public sealed record AutomationMaskOutput(
    string Id,
    string Description,
    string RelativePath,
    bool IsRequired = true);
