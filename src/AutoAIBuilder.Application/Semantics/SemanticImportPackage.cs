using AutoAIBuilder.Domain.Semantics;

namespace AutoAIBuilder.Application.Semantics;

public sealed record SemanticImportPackage(
    SemanticDataset Dataset,
    IReadOnlyList<SemanticPoint> Points,
    IReadOnlyList<SemanticComponent> Components,
    IReadOnlyList<SemanticDirectionDiagnostic> Directions);
