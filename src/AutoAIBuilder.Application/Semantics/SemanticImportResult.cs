using AutoAIBuilder.Domain.Semantics;

namespace AutoAIBuilder.Application.Semantics;

public sealed record SemanticImportResult(
    SemanticDataset Dataset,
    bool ReplacedExistingDataset,
    IReadOnlyList<string> Warnings);
