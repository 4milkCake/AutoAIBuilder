namespace AutoAIBuilder.Application.Automation.Execution;

public sealed record AutomationInputSnapshot(
    string OriginalPath,
    long SizeBytes,
    DateTimeOffset LastWriteTimeUtc,
    string Sha256);
