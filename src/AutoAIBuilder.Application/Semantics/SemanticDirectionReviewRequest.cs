using AutoAIBuilder.Domain.Semantics;

namespace AutoAIBuilder.Application.Semantics;

public sealed record SemanticDirectionReviewRequest(
    string BuilderDirection,
    SemanticReviewStatus Status,
    string? Note);
