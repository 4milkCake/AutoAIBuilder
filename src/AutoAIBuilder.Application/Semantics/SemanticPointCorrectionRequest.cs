using AutoAIBuilder.Domain.Semantics;

namespace AutoAIBuilder.Application.Semantics;

public sealed record SemanticPointCorrectionRequest(
    SemanticDiscipline Discipline,
    string SemanticCode,
    string Description,
    string HeightSourceValue,
    string? Note,
    bool AddToProjectKnowledge = true);
