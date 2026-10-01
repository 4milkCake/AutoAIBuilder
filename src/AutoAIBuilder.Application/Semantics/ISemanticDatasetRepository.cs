using AutoAIBuilder.Domain.Semantics;

namespace AutoAIBuilder.Application.Semantics;

public interface ISemanticDatasetRepository
{
    SemanticWorkspaceSnapshot GetForProject(Guid projectId);

    bool Replace(SemanticImportPackage package);

    void UpdatePointReview(
        Guid projectId,
        Guid pointId,
        SemanticReviewStatus status,
        string? note,
        DateTimeOffset reviewedAt);

    SemanticPoint CorrectPoint(
        Guid projectId,
        Guid pointId,
        SemanticPointCorrectionRequest request,
        DateTimeOffset occurredAt);

    IReadOnlyList<SemanticPoint> FindSimilarPoints(
        Guid projectId,
        Guid pointId);

    int ApplyPointCorrection(
        Guid projectId,
        Guid sourcePointId,
        IReadOnlyList<Guid> targetPointIds,
        string? note,
        DateTimeOffset occurredAt);

    SemanticDirectionDiagnostic ReviewDirection(
        Guid projectId,
        Guid directionId,
        SemanticDirectionReviewRequest request,
        DateTimeOffset occurredAt);

    bool UndoLatestRevision(
        Guid projectId,
        SemanticReviewEntityKind entityKind,
        Guid entityId,
        DateTimeOffset revertedAt);
}
