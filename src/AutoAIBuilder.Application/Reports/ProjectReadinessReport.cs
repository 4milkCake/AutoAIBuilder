namespace AutoAIBuilder.Application.Reports;

public sealed record ProjectReadinessReport(
    Guid ProjectId,
    string ProjectName,
    DateTimeOffset GeneratedAt,
    string SuggestedFileName,
    string SuggestedCsvFileName,
    string SuggestedPdfFileName,
    string Content,
    string CsvContent,
    int PassedCount,
    int WarningCount,
    int ErrorCount,
    bool IsAutomationBlocked);
