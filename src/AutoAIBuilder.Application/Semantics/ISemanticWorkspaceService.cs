using AutoAIBuilder.Domain.Semantics;

namespace AutoAIBuilder.Application.Semantics;

public interface ISemanticWorkspaceService
{
    SemanticWorkspaceSnapshot GetForProject(Guid projectId);

    Task<SemanticImportResult> ImportAsync(
        Guid projectId,
        IReadOnlyList<string> csvPaths,
        CancellationToken cancellationToken = default);

    void UpdatePointReview(
        Guid projectId,
        Guid pointId,
        SemanticReviewStatus status,
        string? note = null);

    SemanticPoint CorrectPoint(
        Guid projectId,
        Guid pointId,
        SemanticPointCorrectionRequest request);

    IReadOnlyList<SemanticPoint> FindSimilarPoints(
        Guid projectId,
        Guid pointId);

    int ApplyPointCorrection(
        Guid projectId,
        Guid sourcePointId,
        IReadOnlyList<Guid> targetPointIds,
        string? note = null);

    SemanticDirectionDiagnostic ReviewDirection(
        Guid projectId,
        Guid directionId,
        SemanticDirectionReviewRequest request);

    bool UndoLatestRevision(
        Guid projectId,
        SemanticReviewEntityKind entityKind,
        Guid entityId);
}
