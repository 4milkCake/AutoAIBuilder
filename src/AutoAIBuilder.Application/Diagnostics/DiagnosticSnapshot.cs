namespace AutoAIBuilder.Application.Diagnostics;

public sealed record DiagnosticSnapshot(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<DiagnosticCheck> Checks,
    IReadOnlyList<DiagnosticLogEntry> RecentLogs)
{
    public int WarningCount =>
        Checks.Count(check => check.Status == DiagnosticStatus.Warning);

    public int ErrorCount =>
        Checks.Count(check => check.Status == DiagnosticStatus.Error);
}
