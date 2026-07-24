namespace AutoAIBuilder.Application.Maintenance;

public sealed record DataBackupResult(
    string FilePath,
    DateTimeOffset CreatedAt,
    long SizeBytes);
