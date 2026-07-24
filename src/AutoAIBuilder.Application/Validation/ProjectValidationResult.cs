namespace AutoAIBuilder.Application.Validation;

public sealed record ProjectValidationResult(
    string Code,
    string Area,
    string Title,
    string Detail,
    string Recommendation,
    ProjectValidationStatus Status);
