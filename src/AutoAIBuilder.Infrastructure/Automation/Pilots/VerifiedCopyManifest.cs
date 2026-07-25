namespace AutoAIBuilder.Infrastructure.Automation.Pilots;

internal sealed record VerifiedCopyManifest(
    string SchemaVersion,
    Guid ExecutionId,
    string PilotId,
    string PilotVersion,
    string SourceFileName,
    string SourceCopySha256,
    string VerifiedCopyRelativePath,
    string VerifiedCopySha256,
    DateTimeOffset CreatedAtUtc);
