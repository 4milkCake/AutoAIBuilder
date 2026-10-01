namespace AutoAIBuilder.Domain.Semantics;

public sealed record SemanticReviewRevision(
    Guid Id,
    Guid DatasetId,
    SemanticReviewEntityKind EntityKind,
    Guid EntityId,
    string EntityExternalId,
    string Action,
    string Summary,
    string? Note,
    DateTimeOffset OccurredAt,
    DateTimeOffset? RevertedAt);
