namespace AutoAIBuilder.Domain.Semantics;

public sealed record SemanticDataset(
    Guid Id,
    Guid ProjectId,
    string DrawingFileName,
    string DrawingPath,
    string SourceVersion,
    string SourceFingerprint,
    IReadOnlyList<string> SourceFiles,
    int PointCount,
    int ElectricalPointCount,
    int HydraulicPointCount,
    int ComponentCount,
    int SemanticLayerCount,
    int DirectionCount,
    int DirectionReviewCount,
    int BuilderValidatedDirectionCount,
    int SymmetryInferredDirectionCount,
    bool MatchesHistoricalBaseline,
    DateTimeOffset ImportedAt,
    DateTimeOffset UpdatedAt);
