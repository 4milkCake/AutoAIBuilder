namespace AutoAIBuilder.Domain.Semantics;

public sealed record SemanticKnowledgeEntry(
    Guid Id,
    Guid ProjectId,
    Guid DatasetId,
    Guid SourcePointId,
    string Signature,
    string BlockName,
    string SemanticLayer,
    string PreviousCode,
    string LearnedCode,
    string LearnedDescription,
    string LearnedHeight,
    int EvidenceCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
