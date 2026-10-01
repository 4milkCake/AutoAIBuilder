namespace AutoAIBuilder.Domain.Semantics;

public sealed record SemanticDirectionDiagnostic(
    Guid Id,
    Guid DatasetId,
    string Version,
    string Handle,
    string CalibrationDirection,
    string Layer,
    string BlockName,
    string EffectiveName,
    double InsertionX,
    double InsertionY,
    double InsertionZ,
    double CenterX,
    double CenterY,
    double CenterZ,
    double RotationDegrees,
    double EffectiveAngleDegrees,
    string SuggestedBuilderDirection,
    string GraphicAngleType,
    double CardinalDistanceDegrees,
    double QuadrantLimitDistanceDegrees,
    bool NeedsReview,
    double RelativeDxCm,
    double RelativeDyCm,
    double RelativeDzCm,
    string OffsetOrigin,
    SemanticReviewStatus ReviewStatus,
    string? CorrectedBuilderDirection,
    string? ReviewNote,
    DateTimeOffset? ReviewedAt)
{
    public string EffectiveBuilderDirection =>
        string.IsNullOrWhiteSpace(CorrectedBuilderDirection)
            ? SuggestedBuilderDirection
            : CorrectedBuilderDirection;
}
