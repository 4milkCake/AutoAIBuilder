namespace AutoAIBuilder.Domain.Recognition;

public sealed record RecognitionSession(
    Guid Id,
    Guid ProjectId,
    string SourceDwgPath,
    string SourceFileName,
    string SourceSha256,
    string EngineName,
    string EngineVersion,
    string ProfileSource,
    int InventoryEntityCount,
    int InventoryInsertCount,
    bool OriginalIntegrityConfirmed,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string RunDirectory,
    string ManifestPath,
    IReadOnlyList<RecognitionCandidate> Candidates,
    RecognitionLegendAnalysis? LegendAnalysis = null,
    int InventoryExpandedInsertCount = 0);
