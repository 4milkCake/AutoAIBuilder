namespace AutoAIBuilder.Application.Validation;

public sealed record ProjectValidationReport(
    Guid ProjectId,
    DateTimeOffset EvaluatedAt,
    IReadOnlyList<ProjectValidationResult> Results,
    bool BlockOnErrors)
{
    public int PassedCount => Results.Count(result => result.Status == ProjectValidationStatus.Passed);

    public int WarningCount => Results.Count(result => result.Status == ProjectValidationStatus.Warning);

    public int ErrorCount => Results.Count(result => result.Status == ProjectValidationStatus.Error);

    public bool IsAutomationBlocked => BlockOnErrors && ErrorCount > 0;
}
