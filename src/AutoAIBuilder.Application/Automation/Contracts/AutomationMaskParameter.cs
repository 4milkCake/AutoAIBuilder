namespace AutoAIBuilder.Application.Automation.Contracts;

public sealed record AutomationMaskParameter(
    string Name,
    string Type,
    string Description,
    bool IsRequired,
    string? DefaultValue = null,
    IReadOnlyList<string>? AllowedValues = null);
