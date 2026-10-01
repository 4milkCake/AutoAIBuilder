using AutoAIBuilder.Application.CadVisualization;
using AutoAIBuilder.Application.Semantics;
using AutoAIBuilder.Domain.Recognition;
using AutoAIBuilder.Domain.Semantics;

namespace AutoAIBuilder.Application.Recognition;

public sealed record RecognitionCorrectionRequest(
    SemanticDiscipline Discipline,
    string SemanticCode,
    string Description,
    string SemanticLayer,
    string Height,
    string? Note);

public interface ICadRecognitionService
{
    CadVisualizationEngineStatus GetEngineStatus();

    RecognitionSession? LoadLatest(Guid projectId);

    Task<RecognitionSession> AnalyzeAsync(
        Guid projectId,
        string sourceDwgPath,
        SemanticWorkspaceSnapshot reference,
        CancellationToken cancellationToken = default);

    RecognitionSession Review(
        Guid projectId,
        Guid sessionId,
        Guid candidateId,
        RecognitionCandidateStatus status,
        RecognitionCorrectionRequest? correction = null,
        string? note = null);
}
