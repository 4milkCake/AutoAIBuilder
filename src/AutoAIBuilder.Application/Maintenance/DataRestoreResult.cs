namespace AutoAIBuilder.Application.Maintenance;

public sealed record DataRestoreResult(
    string SourceFilePath,
    string SafetyBackupFilePath,
    DateTimeOffset RestoredAt);
