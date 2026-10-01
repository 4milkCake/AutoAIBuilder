using AutoAIBuilder.Domain.Semantics;

namespace AutoAIBuilder.Application.Semantics;

public sealed record SemanticWorkspaceSnapshot(
    SemanticDataset? Dataset,
    IReadOnlyList<SemanticPoint> Points,
    IReadOnlyList<SemanticComponent> Components,
    IReadOnlyList<SemanticDirectionDiagnostic> Directions,
    IReadOnlyList<SemanticReviewRevision> Revisions,
    IReadOnlyList<SemanticKnowledgeEntry> KnowledgeEntries)
{
    public static SemanticWorkspaceSnapshot Empty { get; } =
        new(null, [], [], [], [], []);
}
